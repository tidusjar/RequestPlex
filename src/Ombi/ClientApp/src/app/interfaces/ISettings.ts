import { ISettings } from "./ICommon";

export interface IExternalSettings extends ISettings {
  ssl: boolean;
  subDir: string;
  ip: string;
  port: number;
}

export interface IOmbiSettings extends ISettings {
  baseUrl: string;
  collectAnalyticData: boolean;
  wizard: boolean;
  apiKey: string;
  ignoreCertificateErrors: boolean;
  doNotSendNotificationsForAutoApprove: boolean;
  hideRequestsUsers: boolean;
  defaultLanguageCode: string;
  disableHealthChecks: boolean;
  autoDeleteAvailableRequests: boolean;
  autoDeleteAfterDays: number;
}

export interface IUpdateSettings extends ISettings {
  autoUpdateEnabled: boolean;
  username: string;
  password: string;
  processName: string;
  useScript: boolean;
  scriptLocation: string;
  windowsService: boolean;
  windowsServiceName: string;
  isWindows: boolean;
  testMode: boolean;
}

export interface IEmbySettings extends ISettings {
  enable: boolean;
  isJellyfin: boolean;
  servers: IEmbyServer[];
}

export interface IEmbyServer extends IExternalSettings {
  name: string;
  apiKey: string;
  administratorId: string;
  enableEpisodeSearching: boolean;
  serverHostname: string;
}

export interface IPublicInfo {
  serverName: string;
  isJellyfin: boolean;
}

export interface IPlexSettings extends ISettings {
  enable: boolean;
  servers: IPlexServer[];
}

export interface IPlexServer extends IExternalSettings {
  name: string;
  enableEpisodeSearching: boolean;
  plexAuthToken: string;
  machineIdentifier: string;
  episodeBatchSize: number;
  plexSelectedLibraries: IPlexLibrariesSettings[];
}

export interface IPlexLibrariesSettings {
  key: string;
  title: string;
  enabled: boolean;
}

export interface ISonarrSettings extends IExternalSettings {
  apiKey: string;
  enabled: boolean;
  qualityProfile: string;
  qualityProfileAnime: string;
  seasonFolders: boolean;
  rootPath: string;
  rootPathAnime: string;
  fullRootPath: string;
  addOnly: boolean;
  v3: boolean;
  languageProfile: number;
  scanForAvailability: boolean;
}

export interface IRadarrSettings extends IExternalSettings {
  enabled: boolean;
  apiKey: string;
  defaultQualityProfile: number;
  defaultRootPath: string;
  fullRootPath: string;
  addOnly: boolean;
  minimumAvailability: string;
  scanForAvailability: boolean;
}

export interface ILidarrSettings extends IExternalSettings {
  enabled: boolean;
  apiKey: string;
  defaultQualityProfile: string;
  defaultRootPath: string;
  fullRootPath: string;
  metadataProfileId: number;
  languageProfileId: number;
  albumFolder: boolean;
  addOnly: boolean;
}

export interface ILandingPageSettings extends ISettings {
  enabled: boolean;

  noticeEnabled: boolean;
  noticeText: string;

  timeLimit: boolean;
  startDateTime: Date;
  endDateTime: Date;
  expired: boolean;
}

export interface ICustomizationSettings extends ISettings {
  applicationName: string;
  applicationUrl: string;
  logo: string;
  customCss: string;
  enableCustomDonations: boolean;
  customDonationUrl: string;
  customDonationMessage: string;
  recentlyAddedPage: boolean;
  useCustomPage: boolean;
  hideAvailableFromDiscover: boolean;
}

export interface IJobSettings {
  embyContentSync: string;
  sonarrSync: string;
  radarrSync: string;
  plexContentSync: string;
  couchPotatoSync: string;
  automaticUpdater: string;
  userImporter: string;
  sickRageSync: string;
  newsletter: string;
  plexRecentlyAddedSync: string;
  lidarrArtistSync: string;
  issuesPurge: string;
  retryRequests: string;
  mediaDatabaseRefresh: string;
  autoDeleteRequests: string;
}

export interface IIssueSettings extends ISettings {
  enabled: boolean;
  enableInProgress: boolean;
  deleteIssues: boolean;
  daysAfterResolvedToDelete: number;
}

export interface IAuthenticationSettings extends ISettings {
  allowNoPassword: boolean;
  // Password

  requiredDigit: boolean;
  requiredLength: number;
  requiredLowercase: boolean;
  requireNonAlphanumeric: boolean;
  requireUppercase: boolean;
  enableOAuth: boolean;
}

export interface ILdapSettings extends ISettings {
  isEnabled: boolean;
  hostname: string;
  port: number;
  baseDn: string;
  useSsl: boolean;
  useStartTls: boolean;
  skipSslVerify: boolean;
  bindUserDn: string;
  bindUserPassword: string;
  usernameAttribute: string;
  searchFilter: string;
  createUsersAtLogin: boolean;
}

export interface ICustomPage extends ISettings {
  enabled: boolean;
  fontAwesomeIcon: string;
  title: string;
  html: any;
}

export interface IUserManagementSettings extends ISettings {
  importPlexUsers: boolean;
  importPlexAdmin: boolean;
  importEmbyUsers: boolean;
  importLdapUsers: boolean;
  defaultRoles: string[];
  movieRequestLimit: number;
  episodeRequestLimit: number;
  bannedPlexUserIds: string[];
  bannedEmbyUserIds: string[];
}

export interface IAbout {
  version: string;
  branch: string;
  osArchitecture: string;
  osDescription: string;
  processArchitecture: string;
  applicationBasePath: string;
  ombiDatabaseType: string;
  externalDatabaseType: string;
  settingsDatabaseType: string;
  storagePath: string;
  notSupported: boolean;
}

export interface ICouchPotatoSettings extends IExternalSettings {
  enabled: boolean;
  apiKey: string;
  defaultProfileId: string;
  username: string;
  password: string;
}

export interface ISickRageSettings extends IExternalSettings {
  enabled: boolean;
  apiKey: string;
  qualityProfile: string;
  qualities: IDropDownModel[];
}

export interface IDropDownModel {
  value: string;
  display: string;
}

export interface IDogNzbSettings extends ISettings {
  enabled: boolean;
  apiKey: string;
  movies: boolean;
  tvShows: boolean;
}

export interface IIssueCategory extends ISettings {
  value: string;
}

export interface ICronTestModel {
  success: boolean;
  message: string;
}

export interface ICronViewModelBody {
  expression: string;
}

export interface IJobSettingsViewModel {
  result: boolean;
  message: string;
}

export interface IVoteSettings extends ISettings {
  enabled: boolean;
  movieVoteMax: number;
  musicVoteMax: number;
  tvShowVoteMax: number;
}

export interface ITheMovieDbSettings extends ISettings {
    showAdultMovies: boolean;
    excludedKeywordIds: number[];
}
