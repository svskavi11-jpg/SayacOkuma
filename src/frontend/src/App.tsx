import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { DashboardPage } from './features/dashboard/DashboardPage'
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

export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
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
      </Route>
    </Routes>
  )
}