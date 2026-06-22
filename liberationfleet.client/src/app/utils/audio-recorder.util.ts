export class AudioRecorderController {
  private recorder: MediaRecorder | null = null;
  private stream: MediaStream | null = null;
  private chunks: BlobPart[] = [];
  private maxTimeoutId?: ReturnType<typeof setTimeout>;
  private tickIntervalId?: ReturnType<typeof setInterval>;
  private stopResolve: ((blob: Blob | null) => void) | null = null;

  readonly maxDurationMs = 3 * 60 * 1000;

  isRecording = false;
  elapsedMs = 0;

  onStateChange?: () => void;
  onRecordingComplete?: (blob: Blob | null) => void;

  async start(): Promise<void> {
    if (this.isRecording) {
      return;
    }

    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    this.stream = stream;
    this.chunks = [];
    this.elapsedMs = 0;

    const recorder = new MediaRecorder(stream);
    this.recorder = recorder;

    recorder.ondataavailable = event => {
      if (event.data.size > 0) {
        this.chunks.push(event.data);
      }
    };

    recorder.onstop = () => {
      const blob = this.chunks.length > 0
        ? new Blob(this.chunks, { type: recorder.mimeType || 'audio/webm' })
        : null;
      this.cleanup();
      this.stopResolve?.(blob);
      this.stopResolve = null;
      this.onRecordingComplete?.(blob);
    };

    recorder.start();
    this.isRecording = true;
    this.tickIntervalId = setInterval(() => {
      this.elapsedMs += 1000;
      this.notify();
    }, 1000);

    this.maxTimeoutId = setTimeout(() => {
      if (this.isRecording) {
        void this.stop();
      }
    }, this.maxDurationMs);

    this.notify();
  }

  stop(): Promise<Blob | null> {
    if (!this.isRecording || !this.recorder) {
      return Promise.resolve(null);
    }

    return new Promise(resolve => {
      this.stopResolve = resolve;
      if (this.recorder?.state === 'recording') {
        this.recorder.stop();
      } else {
        this.cleanup();
        resolve(null);
      }
    });
  }

  cancel(): void {
    if (!this.isRecording) {
      return;
    }

    this.chunks = [];
    this.stopResolve?.(null);
    this.stopResolve = null;

    if (this.recorder?.state === 'recording') {
      this.recorder.onstop = () => this.cleanup();
      this.recorder.stop();
    } else {
      this.cleanup();
    }
  }

  formatElapsed(): string {
    const totalSeconds = Math.floor(this.elapsedMs / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }

  private cleanup(): void {
    if (this.maxTimeoutId) {
      clearTimeout(this.maxTimeoutId);
      this.maxTimeoutId = undefined;
    }
    if (this.tickIntervalId) {
      clearInterval(this.tickIntervalId);
      this.tickIntervalId = undefined;
    }
    this.stream?.getTracks().forEach(track => track.stop());
    this.stream = null;
    this.recorder = null;
    this.isRecording = false;
    this.elapsedMs = 0;
    this.notify();
  }

  private notify(): void {
    this.onStateChange?.();
  }
}
