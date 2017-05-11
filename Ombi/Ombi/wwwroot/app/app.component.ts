﻿import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationService } from './services/notification.service';
import { SettingsService } from './services/settings.service';
import { AuthService } from './auth/auth.service';
import { IdentityService } from './services/identity.service';

import { ICustomizationSettings } from './interfaces/ISettings';


import template from './app.component.html';

@Component({
    selector: 'ombi',
    moduleId: module.id,
    templateUrl: template
})
export class AppComponent implements OnInit {

    constructor(public notificationService: NotificationService, public authService: AuthService, private router: Router, private settingsService: SettingsService
    , private identityService: IdentityService) {
    }
    customizationSettings: ICustomizationSettings;

    ngOnInit(): void {

        this.settingsService.getCustomization().subscribe(x => this.customizationSettings = x);

        this.router.events.subscribe(() => {
            this.username = localStorage.getItem('currentUser');
            this.showNav = this.authService.loggedIn();
        });

        this.isAdmin = this.identityService.hasRole("Admin");
        this.isPowerUser = this.identityService.hasRole("PowerUser");
    }

    
    logOut() {
        this.authService.logout();
        this.router.navigate(["login"]);
    }

    username:string;
    showNav: boolean;
    isAdmin: boolean;
    isPowerUser:boolean;
}