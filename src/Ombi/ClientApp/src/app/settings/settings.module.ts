import { CommonModule } from "@angular/common";
import { NgModule } from "@angular/core";
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { RouterModule, Routes } from "@angular/router";
// import { TagInputModule } from "ngx-chips";
import { ClipboardModule } from "ngx-clipboard";

import { AuthGuard } from "../auth/auth.guard";
import { AuthService } from "../auth/auth.service";
import {
    CouchPotatoService, EmbyService, IssuesService, JobService, LidarrService, MobileService, NotificationMessageService, PlexService, RadarrService,
    RequestRetryService, SonarrService, TesterService, ValidationService, SystemService, FileDownloadService, TheMovieDbService
} from "../services";

import { PipeModule } from "../pipes/pipe.module";
import { AboutComponent } from "./about/about.component";
import { AuthenticationComponent } from "./authentication/authentication.component";
import { CouchPotatoComponent } from "./couchpotato/couchpotato.component";
import { CustomizationComponent } from "./customization/customization.component";
import { DogNzbComponent } from "./dognzb/dognzb.component";
import { EmbyComponent } from "./emby/emby.component";
import { FailedRequestsComponent } from "./failedrequests/failedrequests.component";
import { IssuesComponent } from "./issues/issues.component";
import { JobsComponent } from "./jobs/jobs.component";
import { LandingPageComponent } from "./landingpage/landingpage.component";
import { LidarrComponent } from "./lidarr/lidarr.component";
import { MassEmailComponent } from "./massemail/massemail.component";
import { DiscordComponent } from "./notifications/discord.component";
import { EmailNotificationComponent } from "./notifications/emailnotification.component";
import { GotifyComponent } from "./notifications/gotify.component";
import { MattermostComponent } from "./notifications/mattermost.component";
import { MobileComponent } from "./notifications/mobile.component";
import { NewsletterComponent } from "./notifications/newsletter.component";
import { NotificationTemplate } from "./notifications/notificationtemplate.component";
import { PushbulletComponent } from "./notifications/pushbullet.component";
import { PushoverComponent } from "./notifications/pushover.component";
import { SlackComponent } from "./notifications/slack.component";
import { TelegramComponent } from "./notifications/telegram.component";
import { WebhookComponent } from "./notifications/webhook.component";
import { OmbiComponent } from "./ombi/ombi.component";
import { PlexComponent } from "./plex/plex.component";
import { RadarrComponent } from "./radarr/radarr.component";
import { SickRageComponent } from "./sickrage/sickrage.component";
import { SonarrComponent } from "./sonarr/sonarr.component";
import { TheMovieDbComponent } from "./themoviedb/themoviedb.component";
import { UpdateComponent } from "./update/update.component";
import { UserManagementComponent } from "./usermanagement/usermanagement.component";
import { VoteComponent } from "./vote/vote.component";
import { WikiComponent } from "./wiki.component";
import { LdapComponent } from "./ldap/ldap.component";

import { SettingsMenuComponent } from "./settingsmenu.component";

import {AutoCompleteModule } from "primeng/autocomplete";
import {CalendarModule } from "primeng/calendar";
import {InputSwitchModule } from "primeng/inputswitch";
import {InputTextModule } from "primeng/inputtext";
import {DialogModule } from "primeng/dialog";
import {MenuModule } from "primeng/menu";
import {RadioButtonModule } from "primeng/radiobutton";
import {TooltipModule } from "primeng/tooltip";

import { MatMenuModule } from "@angular/material/menu";
import { SharedModule } from "../shared/shared.module";
import { HubService } from "../services/hub.service";
import { LogsComponent } from "./logs/logs.component";
import { TwilioComponent } from "./notifications/twilio/twilio.component";
import { WhatsAppComponent } from "./notifications/twilio/whatsapp.component";
import { CloudMobileComponent } from "./notifications/cloudmobile.coponent";
import { CloudMobileService } from "../services/cloudmobile.service";

const routes: Routes = [
    { path: "Ombi", component: OmbiComponent, canActivate: [AuthGuard] },
    { path: "About", component: AboutComponent, canActivate: [AuthGuard] },
    { path: "Plex", component: PlexComponent, canActivate: [AuthGuard] },
    { path: "Emby", component: EmbyComponent, canActivate: [AuthGuard] },
    { path: "Sonarr", component: SonarrComponent, canActivate: [AuthGuard] },
    { path: "Radarr", component: RadarrComponent, canActivate: [AuthGuard] },
    { path: "LandingPage", component: LandingPageComponent, canActivate: [AuthGuard] },
    { path: "Customization", component: CustomizationComponent, canActivate: [AuthGuard] },
    { path: "Email", component: EmailNotificationComponent, canActivate: [AuthGuard] },
    { path: "Discord", component: DiscordComponent, canActivate: [AuthGuard] },
    { path: "Slack", component: SlackComponent, canActivate: [AuthGuard] },
    { path: "Pushover", component: PushoverComponent, canActivate: [AuthGuard] },
    { path: "Pushbullet", component: PushbulletComponent, canActivate: [AuthGuard] },
    { path: "Gotify", component: GotifyComponent, canActivate: [AuthGuard] },
    { path: "Webhook", component: WebhookComponent, canActivate: [AuthGuard] },
    { path: "Mattermost", component: MattermostComponent, canActivate: [AuthGuard] },
    { path: "Twilio", component: TwilioComponent, canActivate: [AuthGuard] },
    { path: "UserManagement", component: UserManagementComponent, canActivate: [AuthGuard] },
    { path: "Update", component: UpdateComponent, canActivate: [AuthGuard] },
    { path: "CouchPotato", component: CouchPotatoComponent, canActivate: [AuthGuard] },
    { path: "DogNzb", component: DogNzbComponent, canActivate: [AuthGuard] },
    { path: "Telegram", component: TelegramComponent, canActivate: [AuthGuard] },
    { path: "Jobs", component: JobsComponent, canActivate: [AuthGuard] },
    { path: "SickRage", component: SickRageComponent, canActivate: [AuthGuard] },
    { path: "Issues", component: IssuesComponent, canActivate: [AuthGuard] },
    { path: "Authentication", component: AuthenticationComponent, canActivate: [AuthGuard] },
    { path: "Ldap", component: LdapComponent, canActivate: [AuthGuard] },
    { path: "Mobile", component: MobileComponent, canActivate: [AuthGuard] },
    { path: "MassEmail", component: MassEmailComponent, canActivate: [AuthGuard] },
    { path: "Newsletter", component: NewsletterComponent, canActivate: [AuthGuard] },
    { path: "Lidarr", component: LidarrComponent, canActivate: [AuthGuard] },
    { path: "Vote", component: VoteComponent, canActivate: [AuthGuard] },
    { path: "TheMovieDb", component: TheMovieDbComponent, canActivate: [AuthGuard] },
    { path: "FailedRequests", component: FailedRequestsComponent, canActivate: [AuthGuard] },
    { path: "Logs", component: LogsComponent, canActivate: [AuthGuard] },
    { path: "CloudMobile", component: CloudMobileComponent, canActivate: [AuthGuard] },
];

@NgModule({
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        RouterModule.forChild(routes),
        MenuModule,
        InputSwitchModule,
        InputTextModule,
        TooltipModule,
        AutoCompleteModule,
        CalendarModule,
        // TagInputModule,
        ClipboardModule,
        PipeModule,
        RadioButtonModule,
        DialogModule,
        SharedModule,
        MatMenuModule
    ],
    declarations: [
        SettingsMenuComponent,
        OmbiComponent,
        PlexComponent,
        EmbyComponent,
        JobsComponent,
        LandingPageComponent,
        CustomizationComponent,
        DiscordComponent,
        SonarrComponent,
        SlackComponent,
        RadarrComponent,
        EmailNotificationComponent,
        NotificationTemplate,
        PushoverComponent,
        MattermostComponent,
        PushbulletComponent,
        GotifyComponent,
        WebhookComponent,
        UserManagementComponent,
        UpdateComponent,
        AboutComponent,
        WikiComponent,
        CouchPotatoComponent,
        DogNzbComponent,
        SickRageComponent,
        TelegramComponent,
        IssuesComponent,
        AuthenticationComponent,
        LdapComponent,
        MobileComponent,
        MassEmailComponent,
        NewsletterComponent,
        LidarrComponent,
        VoteComponent,
        TheMovieDbComponent,
        FailedRequestsComponent,
        LogsComponent,
        TwilioComponent,
        WhatsAppComponent,
        CloudMobileComponent,
    ],
    exports: [
        RouterModule,
    ],
    providers: [
        SonarrService,
        AuthService,
        RadarrService,
        AuthGuard,
        ValidationService,
        TesterService,
        JobService,
        CouchPotatoService,
        IssuesService,
        PlexService,
        EmbyService,
        MobileService,
        NotificationMessageService,
        LidarrService,
        RequestRetryService,
        HubService,
        SystemService,
        FileDownloadService,
        TheMovieDbService,
        CloudMobileService,
    ],

})
export class SettingsModule { }
