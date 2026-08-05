import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export type MediaUploadPhase = 'queued' | 'encrypting' | 'uploading' | 'finalizing' | 'done' | 'error';

export interface MediaUploadJob {
  id: string;
  label: string;
  phase: MediaUploadPhase;
  /** 0–100 overall progress for this job. */
  progress: number;
  error?: string;
  createdAt: number;
}

@Injectable({
  providedIn: 'root'
})
export class MediaUploadQueueService {
  private readonly jobsSubject = new BehaviorSubject<MediaUploadJob[]>([]);
  readonly jobs$: Observable<MediaUploadJob[]> = this.jobsSubject.asObservable();
  readonly activeJobs$: Observable<MediaUploadJob[]> = this.jobs$.pipe(
    map(jobs => jobs.filter(job => job.phase !== 'done' && job.phase !== 'error'))
  );
  readonly hasActive$: Observable<boolean> = this.activeJobs$.pipe(map(jobs => jobs.length > 0));
  readonly aggregateProgress$: Observable<number> = this.activeJobs$.pipe(
    map(jobs => {
      if (jobs.length === 0) {
        return 100;
      }
      return Math.round(jobs.reduce((sum, job) => sum + job.progress, 0) / jobs.length);
    })
  );

  createJob(label: string): string {
    const id = `upload-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const job: MediaUploadJob = {
      id,
      label,
      phase: 'queued',
      progress: 0,
      createdAt: Date.now()
    };
    this.jobsSubject.next([...this.jobsSubject.value, job]);
    return id;
  }

  updateJob(id: string, patch: Partial<Pick<MediaUploadJob, 'phase' | 'progress' | 'error' | 'label'>>): void {
    const jobs = this.jobsSubject.value.map(job => {
      if (job.id !== id) {
        return job;
      }
      return {
        ...job,
        ...patch,
        progress: patch.progress != null
          ? Math.max(0, Math.min(100, patch.progress))
          : job.progress
      };
    });
    this.jobsSubject.next(jobs);

    const updated = jobs.find(job => job.id === id);
    if (updated?.phase === 'done') {
      // Keep completed jobs briefly so the bar can reach 100%, then clear.
      setTimeout(() => this.removeJob(id), 1200);
    }
  }

  removeJob(id: string): void {
    this.jobsSubject.next(this.jobsSubject.value.filter(job => job.id !== id));
  }

  clearFinished(): void {
    this.jobsSubject.next(
      this.jobsSubject.value.filter(job => job.phase !== 'done' && job.phase !== 'error')
    );
  }
}
