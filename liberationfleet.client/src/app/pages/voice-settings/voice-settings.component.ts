import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ToastService } from '../../components/toast/toast.component';
import { VoiceLiveKitService } from '../../services/voice-livekit.service';
import { VoiceDevicePreferences } from '../../models/voice.model';

@Component({
  selector: 'app-voice-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
  templateUrl: './voice-settings.component.html',
  styleUrl: './voice-settings.component.css'
})
export class VoiceSettingsComponent implements OnInit {
  inputDevices: MediaDeviceInfo[] = [];
  outputDevices: MediaDeviceInfo[] = [];
  devicePreferences: VoiceDevicePreferences = { inputDeviceId: '', outputDeviceId: '' };
  loading = true;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private router = inject(Router);
  private voiceLiveKit = inject(VoiceLiveKitService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile/preferences'])
    };

    this.updateSaveButton();
    this.devicePreferences = this.voiceLiveKit.loadDevicePreferences();
    void this.loadDevices();
  }

  async savePreferences() {
    await this.voiceLiveKit.applyDevicePreferences(this.devicePreferences);
    this.toastService.success('Voice device preferences saved');
  }

  private updateSaveButton() {
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      onClick: () => void this.savePreferences()
    };
  }

  private async loadDevices() {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      this.inputDevices = devices.filter(device => device.kind === 'audioinput');
      this.outputDevices = devices.filter(device => device.kind === 'audiooutput');
    } catch {
      this.inputDevices = [];
      this.outputDevices = [];
      this.toastService.error('Unable to list audio devices');
    } finally {
      this.loading = false;
    }
  }
}
