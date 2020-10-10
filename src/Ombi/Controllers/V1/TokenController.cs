using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ombi.Core.Authentication;
using Ombi.Helpers;
using Ombi.Models;
using Ombi.Models.External;
using Ombi.Models.Identity;
using Ombi.Store.Entities;
using Ombi.Store.Repository;

namespace Ombi.Controllers.V1
{
    [ApiV1]
    [Produces("application/json")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        public TokenController(OmbiUserManager um, IOptions<TokenAuthentication> ta, ITokenRepository token,
            IPlexOAuthManager oAuthManager)
        {
            _userManager = um;
            _tokenAuthenticationOptions = ta.Value;
            _token = token;
            _plexOAuthManager = oAuthManager;
        }

        private readonly TokenAuthentication _tokenAuthenticationOptions;
        private readonly ITokenRepository _token;
        private readonly OmbiUserManager _userManager;
        private readonly IPlexOAuthManager _plexOAuthManager;

        /// <summary>
        /// Gets the token.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetToken([FromBody] UserAuthModel model)
        {
            if (!model.UsePlexOAuth)
            {
                var user = await _userManager.FindUser(model.Username);
                if (user == null)
                {
                    return new UnauthorizedResult();
                }

                // Verify Password
                if (await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    return await CreateToken(model.RememberMe, user);
                }
            }
            else
            {
                // Plex OAuth
                // Redirect them to Plex

                var websiteAddress = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}";
                //https://app.plex.tv/auth#?forwardUrl=http://google.com/&clientID=Ombi-Test&context%5Bdevice%5D%5Bproduct%5D=Ombi%20SSO&pinID=798798&code=4lgfd
                var url = await _plexOAuthManager.GetOAuthUrl(model.PlexTvPin.code, websiteAddress);
                if (url == null)
                {
                    return new JsonResult(new
                    {
                        error = "Application URL has not been set"
                    });
                }
                return new JsonResult(new { url = url.ToString(), pinId = model.PlexTvPin.id });
            }

            return new UnauthorizedResult();
        }

        /// <summary>
        /// Returns the Token for the Ombi User if we can match the Plex user with a valid Ombi User
        /// </summary>
        [HttpPost("plextoken")]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetTokenWithPlexToken([FromBody] PlexTokenAuthentication model)
        {
            if (!model.PlexToken.HasValue())
            {
                return BadRequest("Token was not provided");
            }
            var user = await _userManager.GetOmbiUserFromPlexToken(model.PlexToken);
            if (user == null)
            {
                return Unauthorized();
            }
            return await CreateToken(true, user);
        }


        private async Task<IActionResult> CreateToken(bool rememberMe, OmbiUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains(OmbiRoles.Disabled))
            {
                return new UnauthorizedResult();
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("Id", user.Id)
            };
            claims.AddRange(roles.Select(role => new Claim("role", role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_tokenAuthenticationOptions.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);


            var token = new JwtSecurityToken(
                claims: claims,
                expires: rememberMe ? DateTime.Now.AddDays(7) : DateTime.Now.AddDays(1),
                signingCredentials: creds,
                audience: "Ombi", issuer: "Ombi"
            );
            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            if (rememberMe)
            {
                // Save the token so we can refresh it later
                //await _token.CreateToken(new Tokens() {Token = accessToken, User = user});
            }

            user.LastLoggedIn = DateTime.UtcNow;
            try
            {
                await _userManager.UpdateAsync(user);
            }
            catch (Exception)
            {
                
            }

            return new JsonResult(new
            {
                access_token = accessToken,
                expiration = token.ValidTo
            });
        }

        [HttpGet("{pinId:int}")]
        [ProducesResponseType(401)]
        public async Task<IActionResult> OAuth(int pinId)
        {
            var accessToken = await _plexOAuthManager.GetAccessTokenFromPin(pinId);

            if (accessToken.IsNullOrEmpty())
            {
                // Looks like we are not authenticated.
                return new JsonResult(new
                {
                    errorMessage = "Could not authenticate with Plex"
                });
            }

            // Let's look for the users account
            var account = await _plexOAuthManager.GetAccount(accessToken);

            // Get the ombi user
            var user = await _userManager.FindUser(account.user.username);

            if (user == null)
            {
                return new UnauthorizedResult();
            }

            return await CreateToken(true, user);
        }

        /// <summary>
        /// Refreshes the token.
        /// </summary>
        /// <param name="token">The model.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        [HttpPost("refresh")]
        [ProducesResponseType(401)]
        public IActionResult RefreshToken([FromBody] TokenRefresh token)
        {

            // Check if token exists
            var dbToken = _token.GetToken(token.Token).FirstOrDefault();
            if (dbToken == null)
            {
                return new UnauthorizedResult();
            }


            throw new NotImplementedException();
        }

        [HttpPost("requirePassword")]
        public async Task<bool> DoesUserRequireAPassword([FromBody] UserAuthModel model)
        {
            var user = await _userManager.FindUser(model.Username);

            if (user == null)
            {
                return true;
            }

            var requires = await _userManager.RequiresPassword(user);
            return requires;
        }

        public class TokenRefresh
        {
            public string Token { get; set; }
            public string Userename { get; set; }
        }

    }
}