import { Component, OnDestroy, OnInit } from "@angular/core";
import { FormBuilder, FormGroup } from "@angular/forms";
import { Subscription } from 'rxjs';

import { NotificationService } from "../../services/notification.service";
import { SettingsService } from "../../services/settings.service";


@Component({
  templateUrl: "./ldap.component.html",
  styleUrls: ["./ldap.component.scss"],
})
export class LdapComponent implements OnInit, OnDestroy {

  public form: FormGroup;

  private sub: Subscription;

  constructor(
    private settingsService: SettingsService,
    private notificationService: NotificationService,
    private formBuilder: FormBuilder
  ) {}

  public ngOnInit() {
    this.sub = this.settingsService.getLdap().subscribe(ldapSettings => {
        this.form = this.formBuilder.group({
          isEnabled: [ldapSettings.isEnabled],
          hostname: [ldapSettings.hostname],
          port: [ldapSettings.port],
          baseDn: [ldapSettings.baseDn],
          useSsl: [ldapSettings.useSsl],
          useStartTls: [ldapSettings.useStartTls],
          skipSslVerify: [ldapSettings.skipSslVerify],
          bindUserDn: [ldapSettings.bindUserDn],
          bindUserPassword: [ldapSettings.bindUserPassword],
          usernameAttribute: [ldapSettings.usernameAttribute],
          searchFilter: [ldapSettings.searchFilter],
          createUsersAtLogin: [ldapSettings.createUsersAtLogin],
        });
    });
  }

  public ngOnDestroy() {
    this.sub.unsubscribe();
  }

  public onSubmit(form: FormGroup) {
    if (form.invalid) {
        this.notificationService.error("Please check your entered values");
        return;
    }

    this.settingsService.saveLdap(form.value).subscribe(x => {
        if (x) {
            this.notificationService.success("Successfully saved LDAP settings");
        } else {
            this.notificationService.success("There was an error when saving LDAP settings");
        }
    });
  }
}