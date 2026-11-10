import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { LoginPage } from './features/auth/LoginPage'
import { ProtectedRoute, ResidentRoute, StaffRoute } from './features/auth/ProtectedRoute'
import { DashboardPage } from './features/dashboard/DashboardPage'
import { ResidentPage } from './features/resident/ResidentPage'
import { ApartmentsPage } from './features/apartments/ApartmentsPage'
import { InvoicesPage } from './features/invoices/InvoicesPage'
import { PaymentsPage } from './features/payments/PaymentsPage'
import { AidatTariffsPage } from './features/aidat/AidatTariffsPage'
import { AidatInvoicesPage } from './features/aidat/AidatInvoicesPage'
import { FormerResidentsPage } from './features/residents/FormerResidentsPage'
import { ReportsPage } from './features/reports/ReportsPage'
import { BudgetPage } from './features/budget/BudgetPage'
import { SettingsPage } from './features/settings/SettingsPage'
import { NotificationsPage } from './features/notifications/NotificationsPage'
import { BankAccountsPage } from './features/bankAccounts/BankAccountsPage'
import { SuperAdminPage } from './features/admin/SuperAdminPage'
import { RolesPage } from './features/roles/RolesPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route element={<ResidentRoute />}>
            <Route path="/resident" element={<ResidentPage />} />
          </Route>
          <Route element={<StaffRoute />}>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/apartments" element={<ApartmentsPage />} />
            <Route path="/invoices" element={<InvoicesPage />} />
            <Route path="/payments" element={<PaymentsPage />} />
            <Route path="/aidat/tariffs" element={<AidatTariffsPage />} />
            <Route path="/aidat/invoices" element={<AidatInvoicesPage />} />
            <Route path="/former-residents" element={<FormerResidentsPage />} />
            <Route path="/budget" element={<BudgetPage />} />
            <Route path="/bank-accounts" element={<BankAccountsPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/notifications" element={<NotificationsPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/roles" element={<RolesPage />} />
            <Route path="/admin" element={<SuperAdminPage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  )
}