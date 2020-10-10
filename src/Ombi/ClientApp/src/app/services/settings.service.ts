import { PlatformLocation, APP_BASE_HREF } from "@angular/common";
import { HttpClient } from "@angular/common/http";
import { Injectable, Inject } from "@angular/core";
import { Observable } from "rxjs";

import {
    IAbout,
    IAuthenticationSettings,
    ICouchPotatoSettings,
    ICronTestModel,
    ICronViewModelBody,
    ICustomizationSettings,
    IDiscordNotifcationSettings,
    IDogNzbSettings,
    IEmailNotificationSettings,
    IEmbySettings,
    IGotifyNotificationSettings,
    IIssueSettings,
    IJobSettings,
    IJobSettingsViewModel,
    ILandingPageSettings,
    ILidarrSettings,
    IMattermostNotifcationSettings,
    IMobileNotifcationSettings,
    INewsletterNotificationSettings,
    IOmbiSettings,
    IPlexSettings,
    IPushbulletNotificationSettings,
    IPushoverNotificationSettings,
    IRadarrSettings,
    ISickRageSettings,
    ISlackNotificationSettings,
    ISonarrSettings,
    ITelegramNotifcationSettings,
    ITheMovieDbSettings,
    IUpdateSettings,
    IUserManagementSettings,
    IVoteSettings,
    ITwilioSettings,
    IWebhookNotificationSettings,
    ILdapSettings,
} from "../interfaces";

import { ServiceHelpers } from "./service.helpers";

@Injectable()
export class SettingsService extends ServiceHelpers {
    constructor(public http: HttpClient, @Inject(APP_BASE_HREF) href:string) {
        super(http, "/api/v1/Settings", href);
    }

    public about(): Observable<IAbout> {
        return this.http.get<IAbout>(`${this.url}/About/`, {headers: this.headers});
    }

    public getOmbi(): Observable<IOmbiSettings> {
        return this.http.get<IOmbiSettings>(`${this.url}/Ombi/`, {headers: this.headers});
    }

    public getDefaultLanguage(): Observable<string> {
        return this.http.get<string>(`${this.url}/defaultlanguage/`, {headers: this.headers});
    }

    public saveOmbi(settings: IOmbiSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Ombi/`, JSON.stringify(settings), {headers: this.headers});
    }

    public resetOmbiApi(): Observable<string> {
        return this.http.post<string>(`${this.url}/Ombi/resetApi`, {headers: this.headers});
    }

    public getEmby(): Observable<IEmbySettings> {
        return this.http.get<IEmbySettings>(`${this.url}/Emby/`);
    }

    public saveEmby(settings: IEmbySettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Emby/`, JSON.stringify(settings), {headers: this.headers});
    }

    public getPlex(): Observable<IPlexSettings> {
        return this.http.get<IPlexSettings>(`${this.url}/Plex/`, {headers: this.headers});
    }

    public savePlex(settings: IPlexSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Plex/`, JSON.stringify(settings), {headers: this.headers});
    }

    public getSonarr(): Observable<ISonarrSettings> {
        return this.http.get<ISonarrSettings>(`${this.url}/Sonarr`, {headers: this.headers});
    }

    public saveSonarr(settings: ISonarrSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Sonarr`, JSON.stringify(settings), {headers: this.headers});
    }

    public getRadarr(): Observable<IRadarrSettings> {
        return this.http.get<IRadarrSettings>(`${this.url}/Radarr`, {headers: this.headers});
    }

    public saveRadarr(settings: IRadarrSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Radarr`, JSON.stringify(settings), {headers: this.headers});
    }

    public getLidarr(): Observable<ILidarrSettings> {
        return this.http.get<ILidarrSettings>(`${this.url}/Lidarr`, {headers: this.headers});
    }

    public lidarrEnabled(): Observable<boolean> {
        return this.http.get<boolean>(`${this.url}/lidarrenabled`, {headers: this.headers});
    }

    public saveLidarr(settings: ILidarrSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Lidarr`, JSON.stringify(settings), {headers: this.headers});
    }

    public getAuthentication(): Observable<IAuthenticationSettings> {
        return this.http.get<IAuthenticationSettings>(`${this.url}/Authentication`, {headers: this.headers});
    }

    public getClientId(): Observable<string> {
        return this.http.get<string>(`${this.url}/clientid`, {headers: this.headers});
    }

    public saveAuthentication(settings: IAuthenticationSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/Authentication`, JSON.stringify(settings), {headers: this.headers});
    }

    public getLdap(): Observable<ILdapSettings> {
        return this.http.get<ILdapSettings>(`${this.url}/ldap`, {headers: this.headers});
    }

    public saveLdap(settings: ILdapSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/ldap`, JSON.stringify(settings), {headers: this.headers});
    }

    // Using http since we need it not to be authenticated to get the landing page settings
    public getLandingPage(): Observable<ILandingPageSettings> {
        return this.http.get<ILandingPageSettings>(`${this.url}/LandingPage`, {headers: this.headers});
    }

    public saveLandingPage(settings: ILandingPageSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/LandingPage`, JSON.stringify(settings), {headers: this.headers});
    }

    // Using http since we need it not to be authenticated to get the customization settings
    public getCustomization(): Observable<ICustomizationSettings> {
        return this.http.get<ICustomizationSettings>(`${this.url}/customization`, {headers: this.headers});
    }

    public saveCustomization(settings: ICustomizationSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/customization`, JSON.stringify(settings), {headers: this.headers});
    }

    public getEmailNotificationSettings(): Observable<IEmailNotificationSettings> {
        return this.http.get<IEmailNotificationSettings>(`${this.url}/notifications/email`, {headers: this.headers});
    }
    public getEmailSettingsEnabled(): Observable<boolean> {
        return this.http.get<boolean>(`${this.url}/notifications/email/enabled`, {headers: this.headers});
    }

    public saveEmailNotificationSettings(settings: IEmailNotificationSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/notifications/email`, JSON.stringify(settings), {headers: this.headers});
    }

    public getDiscordNotificationSettings(): Observable<IDiscordNotifcationSettings> {
        return this.http.get<IDiscordNotifcationSettings>(`${this.url}/notifications/discord`, {headers: this.headers});
    }

    public getMattermostNotificationSettings(): Observable<IMattermostNotifcationSettings> {
        return this.http.get<IMattermostNotifcationSettings>(`${this.url}/notifications/mattermost`, {headers: this.headers});
    }

    public saveMattermostNotificationSettings(settings: IMattermostNotifcationSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/notifications/mattermost`, JSON.stringify(settings), {headers: this.headers});
    }

    public saveDiscordNotificationSettings(settings: IDiscordNotifcationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/discord`, JSON.stringify(settings), {headers: this.headers});
    }

    public getPushbulletNotificationSettings(): Observable<IPushbulletNotificationSettings> {
        return this.http.get<IPushbulletNotificationSettings>(`${this.url}/notifications/pushbullet`, {headers: this.headers});
    }
    public getPushoverNotificationSettings(): Observable<IPushoverNotificationSettings> {
        return this.http.get<IPushoverNotificationSettings>(`${this.url}/notifications/pushover`, {headers: this.headers});
    }

    public savePushbulletNotificationSettings(settings: IPushbulletNotificationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/pushbullet`, JSON.stringify(settings), {headers: this.headers});
    }
    public savePushoverNotificationSettings(settings: IPushoverNotificationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/pushover`, JSON.stringify(settings), {headers: this.headers});
    }

    public getGotifyNotificationSettings(): Observable<IGotifyNotificationSettings> {
        return this.http.get<IGotifyNotificationSettings>(`${this.url}/notifications/gotify`, { headers: this.headers });
    }
    public saveGotifyNotificationSettings(settings: IGotifyNotificationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/gotify`, JSON.stringify(settings), { headers: this.headers });
    }

    public getWebhookNotificationSettings(): Observable<IWebhookNotificationSettings> {
        return this.http.get<IWebhookNotificationSettings>(`${this.url}/notifications/webhook`, { headers: this.headers });
    }
    public saveWebhookNotificationSettings(settings: IWebhookNotificationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/webhook`, JSON.stringify(settings), { headers: this.headers });
    }

    public getSlackNotificationSettings(): Observable<ISlackNotificationSettings> {
        return this.http.get<ISlackNotificationSettings>(`${this.url}/notifications/slack`, {headers: this.headers});
    }

    public saveSlackNotificationSettings(settings: ISlackNotificationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/slack`, JSON.stringify(settings), {headers: this.headers});
    }

    public getMobileNotificationSettings(): Observable<IMobileNotifcationSettings> {
        return this.http.get<IMobileNotifcationSettings>(`${this.url}/notifications/mobile`, {headers: this.headers});
    }

    public saveMobileNotificationSettings(settings: IMobileNotifcationSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/notifications/mobile`, JSON.stringify(settings), {headers: this.headers});
    }

    public getUpdateSettings(): Observable<IUpdateSettings> {
        return this.http.get<IUpdateSettings>(`${this.url}/update`, {headers: this.headers});
    }

    public saveUpdateSettings(settings: IUpdateSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/update`, JSON.stringify(settings), {headers: this.headers});
    }

    public getUserManagementSettings(): Observable<IUserManagementSettings> {
        return this.http.get<IUserManagementSettings>(`${this.url}/UserManagement`, {headers: this.headers});
    }

    public saveUserManagementSettings(settings: IUserManagementSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/UserManagement`, JSON.stringify(settings), {headers: this.headers});
    }

    public getCouchPotatoSettings(): Observable<ICouchPotatoSettings> {
        return this.http.get<ICouchPotatoSettings>(`${this.url}/CouchPotato`, {headers: this.headers});
    }

    public saveCouchPotatoSettings(settings: ICouchPotatoSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/CouchPotato`, JSON.stringify(settings), {headers: this.headers});
    }

    public getDogNzbSettings(): Observable<IDogNzbSettings> {
        return this.http.get<IDogNzbSettings>(`${this.url}/DogNzb`, {headers: this.headers});
    }

    public saveDogNzbSettings(settings: IDogNzbSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/DogNzb`, JSON.stringify(settings), {headers: this.headers});
    }

    public getTelegramNotificationSettings(): Observable<ITelegramNotifcationSettings> {
        return this.http.get<ITelegramNotifcationSettings>(`${this.url}/notifications/telegram`, {headers: this.headers});
    }

    public saveTelegramNotificationSettings(settings: ITelegramNotifcationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/telegram`, JSON.stringify(settings), {headers: this.headers});
    }

    public getTwilioSettings(): Observable<ITwilioSettings> {
        return this.http.get<ITwilioSettings>(`${this.url}/notifications/twilio`, {headers: this.headers});
    }

    public saveTwilioSettings(settings: ITwilioSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/twilio`, JSON.stringify(settings), {headers: this.headers});
    }

    public getJobSettings(): Observable<IJobSettings> {
        return this.http.get<IJobSettings>(`${this.url}/jobs`, {headers: this.headers});
    }

    public saveJobSettings(settings: IJobSettings): Observable<IJobSettingsViewModel> {
        return this.http
            .post<IJobSettingsViewModel>(`${this.url}/jobs`, JSON.stringify(settings), {headers: this.headers});
    }

    public testCron(body: ICronViewModelBody): Observable<ICronTestModel> {
        return this.http
            .post<ICronTestModel>(`${this.url}/testcron`, JSON.stringify(body), {headers: this.headers});
    }

    public getSickRageSettings(): Observable<ISickRageSettings> {
        return this.http.get<ISickRageSettings>(`${this.url}/sickrage`, {headers: this.headers});
    }

    public saveSickRageSettings(settings: ISickRageSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/sickrage`, JSON.stringify(settings), {headers: this.headers});
    }

    public getIssueSettings(): Observable<IIssueSettings> {
        return this.http.get<IIssueSettings>(`${this.url}/issues`, {headers: this.headers});
    }

    public issueEnabled(): Observable<boolean> {
        return this.http.get<boolean>(`${this.url}/issuesenabled`, {headers: this.headers});
    }

    public saveIssueSettings(settings: IIssueSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/issues`, JSON.stringify(settings), {headers: this.headers});
    }

    public getVoteSettings(): Observable<IVoteSettings> {
        return this.http.get<IVoteSettings>(`${this.url}/vote`, {headers: this.headers});
    }

    public voteEnabled(): Observable<boolean> {
        return this.http.get<boolean>(`${this.url}/voteenabled`, {headers: this.headers});
    }

    public saveVoteSettings(settings: IVoteSettings): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/vote`, JSON.stringify(settings), {headers: this.headers});
    }

    public getTheMovieDbSettings(): Observable<ITheMovieDbSettings> {
        return this.http.get<ITheMovieDbSettings>(`${this.url}/themoviedb`, {headers: this.headers});
    }

    public saveTheMovieDbSettings(settings: ITheMovieDbSettings) {
        return this.http.post<boolean>(`${this.url}/themoviedb`, JSON.stringify(settings), {headers: this.headers});
    }

    public getNewsletterSettings(): Observable<INewsletterNotificationSettings> {
        return this.http.get<INewsletterNotificationSettings>(`${this.url}/notifications/newsletter`, {headers: this.headers});
    }

    public updateNewsletterDatabase(): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/notifications/newsletterdatabase`, {headers: this.headers});
    }

    public saveNewsletterSettings(settings: INewsletterNotificationSettings): Observable<boolean> {
        return this.http
            .post<boolean>(`${this.url}/notifications/newsletter`, JSON.stringify(settings), {headers: this.headers});
    }
    public verifyUrl(url: string): Observable<boolean> {
        return this.http.post<boolean>(`${this.url}/customization/urlverify`, JSON.stringify({url}), {headers: this.headers});
    }
}
