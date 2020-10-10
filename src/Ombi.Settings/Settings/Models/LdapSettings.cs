namespace Ombi.Settings.Settings.Models
{
    public class LdapSettings : Settings
    {
        public LdapSettings()
        {
            IsEnabled = false;
            CreateUsersAtLogin = true;
            Hostname = "ldap-server.example.tld";
            BaseDn = "o=domains,dc=example,dc=tld";
            Port = 389;
            UsernameAttribute = "uid";
            SearchFilter = "(memberOf=cn=Users,dc=example,dc=tld)";
            BindUserDn = "cn=BindUser,dc=example,dc=tld";
            BindUserPassword = "password";
            UseSsl = true;
            UseStartTls = false;
            SkipSslVerify = false;
        }

        /// <summary>
        /// Gets or sets whether LDAP is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether users should be automatically created at login.
        /// </summary>
        public bool CreateUsersAtLogin { get; set; }

        /// <summary>
        /// Gets or sets the ldap server ip or url.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Gets or sets the ldap base search dn.
        /// </summary>
        public string BaseDn { get; set; }

        /// <summary>
        /// Gets or sets the ldap port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the ldap username attribute.
        /// </summary>
        public string UsernameAttribute { get; set; }

        /// <summary>
        /// Gets or sets the ldap user search filter.
        /// </summary>
        public string SearchFilter { get; set; }

        /// <summary>
        /// Gets or sets the ldap bind user dn.
        /// </summary>
        public string BindUserDn { get; set; }

        /// <summary>
        /// Gets or sets the ldap bind user password.
        /// </summary>
        public string BindUserPassword { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use ssl when connecting to the ldap server.
        /// </summary>
        public bool UseSsl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use StartTls when connecting to the ldap server.
        /// </summary>
        public bool UseStartTls { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip ssl verification.
        /// </summary>
        public bool SkipSslVerify { get; set; }
    }
}
