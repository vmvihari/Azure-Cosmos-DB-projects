import { Component, OnInit, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { CommonModule } from '@angular/common';

interface Claim {
  id: string;
  providerId: string;
  patientId: string;
  submittedDate: Date;
  status: string;
  totalAmount: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dashboard-container">
      <h2>Real-Time Healthcare Claims Dashboard</h2>
      <div class="status-indicator">
        <span [class]="connectionStatus === 'Connected' ? 'green' : 'red'"></span>
        SignalR Status: {{ connectionStatus }}
      </div>

      <div class="claims-grid">
        <div class="claim-card" *ngFor="let claim of recentClaims">
          <div class="card-header">
            <h3>Claim: {{ claim.id.substring(0, 8) }}...</h3>
            <span class="badge" [ngClass]="claim.status.toLowerCase()">{{ claim.status }}</span>
          </div>
          <div class="card-body">
            <p><strong>Provider:</strong> {{ claim.providerId }}</p>
            <p><strong>Patient:</strong> {{ claim.patientId }}</p>
            <p><strong>Amount:</strong> {{ claim.totalAmount | currency }}</p>
            <p><strong>Updated:</strong> {{ claim.submittedDate | date:'short' }}</p>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-container { padding: 20px; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
    .status-indicator { margin-bottom: 20px; font-weight: bold; display: flex; align-items: center; gap: 8px; }
    .green { width: 10px; height: 10px; border-radius: 50%; background-color: #28a745; display: inline-block; }
    .red { width: 10px; height: 10px; border-radius: 50%; background-color: #dc3545; display: inline-block; }
    .claims-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 15px; }
    .claim-card { border: 1px solid #ddd; border-radius: 8px; padding: 15px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: transform 0.2s; }
    .claim-card:hover { transform: translateY(-5px); }
    .card-header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #eee; padding-bottom: 10px; margin-bottom: 10px; }
    .badge { padding: 5px 10px; border-radius: 12px; font-size: 0.85em; font-weight: bold; color: white; }
    .pending { background-color: #ffc107; color: black; }
    .approved { background-color: #28a745; }
    .rejected { background-color: #dc3545; }
    .under-review { background-color: #17a2b8; }
  `]
})
export class DashboardComponent implements OnInit, OnDestroy {
  private hubConnection: signalR.HubConnection | undefined;
  public connectionStatus = 'Disconnected';
  public recentClaims: Claim[] = [];

  ngOnInit(): void {
    // Connect to the Azure Function hosting the SignalR negotiation endpoint
    // In a real app, this URL would come from environment variables
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:7071/api') // Points to the Azure Functions API
      .withAutomaticReconnect()
      .build();

    this.hubConnection.start()
      .then(() => {
        this.connectionStatus = 'Connected';
        console.log('SignalR connection established.');
      })
      .catch(err => {
        this.connectionStatus = 'Failed to connect';
        console.error('Error while establishing connection', err);
      });

    // Listen for the 'claimUpdated' event broadcasted by the Cosmos DB Change Feed function
    this.hubConnection.on('claimUpdated', (updatedClaim: Claim) => {
      console.log('Real-time claim update received:', updatedClaim);
      
      // Check if we already have this claim in the array
      const index = this.recentClaims.findIndex(c => c.id === updatedClaim.id);
      
      if (index !== -1) {
        // Update existing
        this.recentClaims[index] = updatedClaim;
      } else {
        // Add new (insert at beginning)
        this.recentClaims.unshift(updatedClaim);
        
        // Keep only the most recent 50 for the dashboard
        if (this.recentClaims.length > 50) {
          this.recentClaims.pop();
        }
      }
    });
  }

  ngOnDestroy(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }
}
