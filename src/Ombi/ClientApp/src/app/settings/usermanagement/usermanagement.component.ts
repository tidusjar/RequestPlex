import { Component, OnInit } from "@angular/core";

import { ICheckbox, IUserManagementSettings } from "../../interfaces";
import { IUsersModel } from "../../interfaces";
import { EmbyService, IdentityService, JobService, NotificationService, PlexService, SettingsService } from "../../services";

@Component({
    templateUrl: "./usermanagement.component.html",
    styleUrls: ["./usermanagement.component.scss"]
})
export class UserManagementComponent implements OnInit {

    public plexEnabled: boolean;
    public embyEnabled: boolean;
    public ldapEnabled: boolean;
    public settings: IUserManagementSettings;
    public claims: ICheckbox[];

    public plexUsers: IUsersModel[];
    public filteredPlexUsers: IUsersModel[];
    public bannedPlexUsers: IUsersModel[] = [];

    public embyUsers: IUsersModel[];
    public filteredEmbyUsers: IUsersModel[];
    public bannedEmbyUsers: IUsersModel[] = [];

    public enableImportButton = false;

    constructor(private readonly settingsService: SettingsService,
                private readonly notificationService: NotificationService,
                private readonly identityService: IdentityService,
                private readonly plexService: PlexService,
                private readonly jobService: JobService,
                private readonly embyService: EmbyService) {
    }

    public ngOnInit(): void {
        this.settingsService.getUserManagementSettings().subscribe(x => {
            this.settings = x;

            if(x.importEmbyUsers || x.importPlexUsers || x.importLdapUsers) {
                this.enableImportButton = true;
            }

            this.plexService.getFriends().subscribe(f => {
                this.plexUsers = f;
                this.plexUsers.forEach((plex) => {
                    const isExcluded = this.settings.bannedPlexUserIds.some((val) => {
                        return plex.id === val;
                    });
                    if (isExcluded) {
                        this.bannedPlexUsers.push(plex);
                    }
                });
            });

            this.embyService.getUsers().subscribe(f => {
                this.embyUsers = f;
                this.embyUsers.forEach((emby) => {
                    const isExcluded = this.settings.bannedPlexUserIds.some((val) => {
                        return emby.id === val;
                    });
                    if (isExcluded) {
                        this.bannedEmbyUsers.push(emby);
                    }
                });
            });

            this.identityService.getAllAvailableClaims().subscribe(c => {

                this.claims = c;
                this.claims.forEach((claim) => {
                    if (this.settings.defaultRoles) {
                        const hasClaim = this.settings.defaultRoles.some((item) => {
                            return item === claim.value;
                        });
                        claim.enabled = hasClaim;
                    }
                });
            });
        });
        this.settingsService.getPlex().subscribe(x => this.plexEnabled = x.enable);
        this.settingsService.getEmby().subscribe(x => this.embyEnabled = x.enable);
        this.settingsService.getLdap().subscribe(x => this.ldapEnabled = x.isEnabled);
    }

    public submit(): void {
        const enabledClaims = this.claims.filter((claim) => {
            return claim.enabled;
        });
        this.settings.defaultRoles = enabledClaims.map((claim) => claim.value);
        this.settings.bannedPlexUserIds = this.bannedPlexUsers.map((u) => u.id);
        this.settings.bannedEmbyUserIds = this.bannedEmbyUsers.map((u) => u.id);

        if(this.settings.importEmbyUsers || this.settings.importPlexUsers) {
            this.enableImportButton = true;
        }

        this.settingsService.saveUserManagementSettings(this.settings).subscribe(x => {
            if (x === true) {
                this.notificationService.success("Successfully saved the User Management Settings");
            } else {
                this.notificationService.success( "There was an error when saving the Ombi settings");
            }
        });
    }

    public filterPlexList(event: any) {
        this.filteredPlexUsers = this.filter(event.query, this.plexUsers);
    }

    public filterEmbyList(event: any) {
        this.filteredEmbyUsers = this.filter(event.query, this.embyUsers);
    }

    public runImporter(): void {

        this.jobService.runPlexImporter().subscribe();
        this.jobService.runEmbyImporter().subscribe();
        this.jobService.runLdapImporter().subscribe();
    }

    private filter(query: string, users: IUsersModel[]): IUsersModel[] {
        return users.filter((val) => {
            return val.username.toLowerCase().indexOf(query.toLowerCase()) === 0;
        });
    }
}
