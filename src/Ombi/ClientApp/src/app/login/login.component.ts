import { Component, OnDestroy, OnInit, Inject } from "@angular/core";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import { TranslateService } from "@ngx-translate/core";

import { APP_BASE_HREF } from "@angular/common";
import { AuthService } from "../auth/auth.service";
import { IAuthenticationSettings, ICustomizationSettings } from "../interfaces";
import { PlexTvService } from "../services";
import { SettingsService } from "../services";
import { StatusService } from "../services";

import { DomSanitizer } from "@angular/platform-browser";
import { ImageService } from "../services";

import { fadeInOutAnimation } from "../animations/fadeinout";
import { StorageService } from "../shared/storage/storage-service";
import { MatSnackBar } from "@angular/material/snack-bar";

@Component({
    templateUrl: "./login.component.html",
    animations: [fadeInOutAnimation],
    styleUrls: ["./login.component.scss"],
})
export class LoginComponent implements OnDestroy, OnInit {

    public form: FormGroup;
    public customizationSettings: ICustomizationSettings;
    public authenticationSettings: IAuthenticationSettings;
    public plexEnabled: boolean;
    public background: any;
    public landingFlag: boolean;
    public baseUrl: string;
    public loginWithOmbi: boolean;
    public pinTimer: any;
    public oauthLoading: boolean;

    public get appName(): string {
        if (this.customizationSettings.applicationName) {
            return this.customizationSettings.applicationName;
        } else {
            return "Ombi";
        }
    }

    public get appNameTranslate(): object {
        return { appName: this.appName };
    }
    private timer: any;
    private clientId: string;

    private errorBody: string;
    private errorValidation: string;
    private href: string;

    private oAuthWindow: Window|null;

    constructor(private authService: AuthService, private router: Router, private status: StatusService,
                private fb: FormBuilder, private settingsService: SettingsService, private images: ImageService, private sanitizer: DomSanitizer,
                private route: ActivatedRoute, @Inject(APP_BASE_HREF) href:string, private translate: TranslateService, private plexTv: PlexTvService,
                private store: StorageService, private readonly notify: MatSnackBar) {
        this.href = href;
                    this.route.params
            .subscribe((params: any) => {
                this.landingFlag = params.landing;
                if (!this.landingFlag) {
                    this.settingsService.getLandingPage().subscribe(x => {
                        if (x.enabled && !this.landingFlag) {
                            this.router.navigate(["landingpage"]);
                        }
                    });
                }
            });

        this.form = this.fb.group({
            username: [""],
            password: [""],
            rememberMe: [false],
        });

        this.status.getWizardStatus().subscribe(x => {
            if (!x.result) {
                this.router.navigate(["Wizard"]);
            }
        });

        if (authService.loggedIn()) {
            this.router.navigate(["/"]);
        }
    }

    public ngOnInit() {
        this.settingsService.getAuthentication().subscribe(x => this.authenticationSettings = x);
        this.settingsService.getClientId().subscribe(x => this.clientId = x);
        this.settingsService.getCustomization().subscribe(x => this.customizationSettings = x);
        this.images.getRandomBackground().subscribe(x => {
            this.background = this.sanitizer.bypassSecurityTrustStyle("linear-gradient(-10deg, transparent 20%, rgba(0,0,0,0.7) 20.0%, rgba(0,0,0,0.7) 80.0%, transparent 80%),url(" + x.url + ")");
        });
        this.timer = setInterval(() => {
            this.cycleBackground();
        }, 30000);

        const base = this.href;
        if (base.length > 1) {
            this.baseUrl = base;
        }

        this.translate.get("Login.Errors.IncorrectCredentials").subscribe(x => this.errorBody = x);
        this.translate.get("Common.Errors.Validation").subscribe(x => this.errorValidation = x);
    }

    public onSubmit(form: FormGroup) {
        if (form.invalid) {
            this.notify.open(this.errorValidation, "OK", {
                duration: 3000
            });
            return;
        }
        const value = form.value;
        const user = { password: value.password, username: value.username, rememberMe: value.rememberMe, usePlexOAuth: false, plexTvPin: { id: 0, code: "" } };
        this.authService.requiresPassword(user).subscribe(x => {
            if (x && this.authenticationSettings.allowNoPassword) {
                // Looks like this user requires a password
                this.authenticationSettings.allowNoPassword = false;
                return;
            }
            this.authService.login(user)
                .subscribe(x => {
                    this.store.save("id_token", x.access_token);

                    if (this.authService.loggedIn()) {
                        this.ngOnDestroy();
                        this.router.navigate(["/"]);
                    } else {
                        this.notify.open(this.errorBody, "OK", {
                            duration: 3000
                        });
                    }

                }, err => {
                this.notify.open(this.errorBody, "OK", {
                    duration: 3000
                })
            });
        });
    }

    public oauth() {
        this.oAuthWindow = window.open(window.location.toString(), "_blank", `toolbar=0,
        location=0,
        status=0,
        menubar=0,
        scrollbars=1,
        resizable=1,
        width=500,
        height=500`);
        this.plexTv.GetPin(this.clientId, this.appName).subscribe((pin: any) => {

            this.authService.login({ usePlexOAuth: true, password: "", rememberMe: true, username: "", plexTvPin: pin }).subscribe(x => {
                this.oAuthWindow!.location.replace(x.url);

                this.pinTimer = setInterval(() => {

                    this.oauthLoading = true;
                    this.getPinResult(x.pinId);
                }, 4000);
            });
        });
    }

    public getPinResult(pinId: number) {
        this.authService.oAuth(pinId).subscribe(x => {
            if(x.access_token) {
              this.store.save("id_token", x.access_token);

              if (this.authService.loggedIn()) {
                  this.ngOnDestroy();

                  if(this.oAuthWindow) {
                    this.oAuthWindow.close();
                  }
                  this.oauthLoading = false;
                  this.router.navigate(["search"]);
                  return;
              }
          }

          }, err => {
              console.log(err);
              this.notify.open(err.body, "OK", {
                duration: 3000
            });

              this.router.navigate(["login"]);
          });
    }

    public ngOnDestroy() {
        clearInterval(this.timer);
        clearInterval(this.pinTimer);
    }

    private cycleBackground() {
        this.images.getRandomBackground().subscribe(x => {
            this.background = "";
        });
        this.images.getRandomBackground().subscribe(x => {
            this.background = this.sanitizer
                .bypassSecurityTrustStyle("linear-gradient(-10deg, transparent 20%, rgba(0,0,0,0.7) 20.0%, rgba(0,0,0,0.7) 80.0%, transparent 80%), url(" + x.url + ")");
        });
    }
}
