// RFC 7807 ProblemDetails mirror (Faz 4 API hata standardi).
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export interface ApartmentMeterItem {
  id: string
  serialNumber: string
  meterType: string
  mbusPrimaryAddress: number | null
  isActive: boolean
}

export interface ApartmentListItem {
  id: string
  doorNumber: string
  title: string
  activeSquareMeters: number | null
  /** Etap 6.7: aktif kat maliki (SCD Type 2) */
  ownerName: string | null
  /** Etap 6.7: aktif kalorimetre (en yeni CreatedOn) */
  activeMeterId: string | null
  /** Etap 6.7: son okuma endeksi (aktif kalorimetre) */
  lastIndexValue: number | null
  /** Etap 6.7: son okuma tarihi */
  lastReadingDateUtc: string | null
  /** Etap 6.8: devir bakiyesi — anapara ve geçmiş faiz ayrı */
  openingBalancePrincipal?: number
  openingBalanceInterest?: number
  openingBalanceDate?: string | null
  openingBalanceOwnerId?: string | null
  /** Faz 6.9: daireye atanmış aidat tarifesı */
  aidatTariffId?: string | null
  aidatTariffName?: string | null
  /** Faz 7.1: aktif sakin bilgileri (dropdown etiketleri + iletişim) */
  kiraciName?: string | null
  evSahibiPhoneNumber?: string | null
  evSahibiEmail?: string | null
  evSahibiIdentityNumber?: string | null
  evSahibiNotes?: string | null
  kiraciPhoneNumber?: string | null
  kiraciEmail?: string | null
  kiraciIdentityNumber?: string | null
  kiraciNotes?: string | null
  /** Güncel cari bakiye. Pozitif borç, negatif alacak. */
  currentBalance: number
  /** Borçlu, Alacaklı veya Borcu Yok */
  balanceStatus: string
  /** Kalan aidat borcu (anapara + faiz). */
  aidatDebt: number
  /** Kalan ısınma borcu (anapara + faiz). */
  heatingDebt: number
  meters: ApartmentMeterItem[]
}

export interface GasInvoiceListItem {
  id: string
  invoiceNumber: string
  periodFrom: string
  periodTo: string
  totalAmount: number
  hasCalculation: boolean
  isLocked: boolean
  /** Faz 6.8: son ödeme tarihi (vade) */
  dueDate: string
  /** Faz 6.8: doğalgaz firmasına ödendi mi */
  isPaid: boolean
  /** Faz 6.8: ödeme tarihi */
  paidOn: string | null
}

/** Etap 6.7: fatura düzenleme detayı */
export interface GasInvoiceDetail {
  id: string
  invoiceNumber: string
  subscriberNumber: string
  masterMeterSerial: string
  readingDateFrom: string
  readingDateTo: string
  issuedOn: string
  correctedVolumeM3: number
  totalEnergyKWh: number
  pressureCorrectionFactor: number
  consumptionAmount: number
  consumptionVat: number
  distributionAmount: number
  distributionVat: number
  totalAmount: number
  note: string | null
  hasCalculation: boolean
  isLocked: boolean
}

/** Etap 6.7: fatura düzenleme yanıtı — hesaplaması varsa yeniden hesaplama uyarısı */
export interface UpdateGasInvoiceResult {
  invoiceId: string
  hasCalculationWarning: boolean
}

export interface CalculationRow {
  apartmentInvoiceId: string
  apartmentId: string
  doorNumber: string
  apartmentTitle: string
  squareMetersUsed: number
  consumptionKWhUsed: number
  fixedAmount: number
  consumptionAmount: number
  totalAmount: number
  isMeterReadFailed: boolean
}

export interface CalculationResultResponse {
  gasInvoiceId: string
  invoiceNumber: string
  invoiceTotalAmount: number
  totalDistributedAmount: number
  reconciled: boolean
  rows: CalculationRow[]
}

export interface CalculateDistributionResult {
  apartmentCount: number
  failedMeterCount: number
  totalDistributedAmount: number
  invoiceTotalAmount: number
  reconciled: boolean
  gasInvoiceId: string
  chargedApartmentIds: string[]
  canNotify: boolean
  notifyBlockReason: string | null
}

export interface AddGasInvoiceRequest {
  invoiceNumber: string
  subscriberNumber: string
  masterMeterSerial: string
  readingDateFrom: string
  readingDateTo: string
  issuedOn: string
  correctedVolumeM3: number
  totalEnergyKWh: number
  pressureCorrectionFactor: number
  consumptionAmount: number
  consumptionVat: number
  distributionAmount: number
  distributionVat: number
  totalAmount: number
}

export interface AppSettingsDTO {
  companyName: string
  buildingName: string
  fixedCostRatio: number
  consumptionRatio: number
  meterConnectionType: string
  tcpHost: string | null
  tcpPort: number
  usbPortName: string | null
  usbBaudRate: number
}

export interface NoticeSettingsDTO {
  leftTitle: string | null
  rightTitle: string | null
  dueDateDays: number
  readingServiceFee: number
  defaultNote: string | null
  defaultSubscriberNumber: string | null
  buildingAddress: string
  hasLeftLogo: boolean
  hasRightLogo: boolean
  logoLeftHeightMm: number
  logoRightHeightMm: number
  showDebtBreakdownOnPdf: boolean
  showDebtTableOnPdf: boolean
}

export interface MbusConnectionTestResult {
  success: boolean
  message: string
  durationMs: number
}

export interface MeterReadingCycleResult {
  connectionType: string
  totalMeters: number
  readSuccessCount: number
  readFailureCount: number
  insertedCount: number
  skippedDuplicateCount: number
  skippedDeviceErrorCount: number
  unmatchedSerialCount: number
  lockedPeriodSkippedCount: number
}

/** Etap 6.6: Manuel okuma girişi */
export interface AddManualMeterReadingRequest {
  apartmentId: string
  meterId: string
  readingDate: string
  indexValue: number
}

export interface ManualReadingResult {
  readingId: string
  indexValue: number
  periodConsumptionKWh: number
  isInitialReading: boolean
  warnings: string[]
}

/** Etap 6.6: Excel okuma içe aktarma */
export interface ImportReadingsResult {
  importedCount: number
  skippedDuplicateCount: number
  unmatchedApartmentCount: number
  rowErrors: string[]
}

export interface ImportMetersFromExcelResult {
  importedCount: number
  skippedExistingCount: number
  unmatchedApartmentCount: number
  initialReadingsCreated: number
  /** Etap 6.7: SCD Type 2 kat maliki kayıtları */
  ownersSetCount: number
  /** Etap 6.7: SCD Type 2 m² güncellemeleri */
  dimensionsUpdatedCount: number
  rowErrors: string[]
}

export interface BankDashboardAccount {
  id: string
  bankName: string
  accountName: string
  openingBalance: number
  moneyIn: number
  moneyOut: number
  currentBalance: number
}

export interface BankDashboardMonth {
  month: number
  moneyIn: number
  moneyOut: number
}

export interface BankDashboard {
  year: number
  totalOpeningBalance: number
  totalMoneyIn: number
  totalMoneyOut: number
  totalCurrentBalance: number
  accounts: BankDashboardAccount[]
  months: BankDashboardMonth[]
}

export interface DashboardSummary {
  buildingName: string
  apartmentCount: number
  activeMeterCount: number
  lastInvoiceId: string | null
  lastInvoiceNumber: string | null
  lastInvoiceTotalAmount: number | null
  lastInvoicePeriodFrom: string | null
  lastInvoicePeriodTo: string | null
  lastInvoiceReconciled: boolean | null
  lastLossPercent: number | null
  lastCalculationFailedMeterCount: number
}

/** Etap 6.7: Kat maliki SCD Type 2 geçmiş kaydı */
export interface ApartmentOwnerHistoryItem {
  id: string
  ownerName: string
  validFrom: string
  validTo: string | null
  residentType: 'EvSahibi' | 'Kiraci'
  phoneNumber: string | null
  leaseStartDate: string | null
  leaseEndDate: string | null
}

export interface UpdateApartmentOwnerRequest {
  ownerName: string
}

export interface UpdateApartmentOwnerResult {
  apartmentId: string
  ownerName: string
  validFrom: string
  changed: boolean
}

/** Etap 6.7: Daire tüketim analizi */
export interface MonthlyConsumptionItem {
  year: number
  month: number
  monthName: string
  consumptionKWh: number
  readingCount: number
}

export interface AnalysisReadingItem {
  readingId: string
  readingDateUtc: string
  indexValue: number
  consumptionKWh: number
  source: string
  isInitialReading: boolean
}

export interface ApartmentConsumptionAnalysis {
  apartmentId: string
  doorNumber: string
  title: string
  year: number | null
  from: string | null
  to: string | null
  totalConsumptionKWh: number
  averageMonthlyConsumptionKWh: number
  readingCount: number
  monthly: MonthlyConsumptionItem[]
  readings: AnalysisReadingItem[]
}

/** Etap 6.7: Okuma listesi (düzenleme Modal'ı veri kaynağı) */
export interface MeterReadingListItem {
  id: string
  readingDateUtc: string
  indexValue: number
  consumptionKWh: number
  source: string
  isInitialReading: boolean
  isLockedPeriod: boolean
  auditLogCount: number
}

/** Etap 6.7: Okuma düzeltme (tek/toplu) */
export interface MeterReadingCorrectionItem {
  readingId: string
  newIndexValue: number
  reason: string
  changedBy?: string | null
}

export interface UpdateMeterReadingRequest {
  items: MeterReadingCorrectionItem[]
}

export interface UpdateMeterReadingResult {
  updatedCount: number
  rowErrors: string[]
}

/** Etap 6.7: Okuma düzeltme denetim kaydı */
export interface MeterReadingAuditLogItem {
  id: string
  oldIndexValue: number
  newIndexValue: number
  oldConsumptionKWh: number
  newConsumptionKWh: number
  reason: string
  changedBy: string
  changedOnUtc: string
}

export interface InvoiceFinanceResult {
  invoiceId: string
  dueDate: string
  isPaid: boolean
  paidOn: string | null
}

/** Faz 6.8: gecikme faizi ayarları (yıllık basit faiz, günlük işletim) */
export interface InterestSettingsDTO {
  isInterestEnabled: boolean
  interestPeriod: string
  interestRate: number
}

// ── Faz 6.8: Tahsilat ────────────────────────────────────────────────────────

export interface PaymentListItem {
  id: string
  apartmentId: string
  paymentDate: string       // "yyyy-MM-dd"
  amount: number
  description: string | null
  receivedBy: string
  isCancelled: boolean
  cancelledOn: string | null
  cancellationReason: string | null
  createdOn: string
  /** Boşsa tahsilat genel FIFO ile mahsup edilmiştir. */
  targetDebtType?: PaymentTargetDebtType | null
  bankAccountId?: string | null
  bankAccountLabel?: string | null
}

/** Backend LedgerDebtType (JSON enum adı). Otomatik FIFO için alan gönderilmez. */
export type PaymentTargetDebtType =
  | 'Aidat'
  | 'Invoice'
  | 'OpeningBalancePrincipal'
  | 'OpeningBalanceInterest'

export interface RecordPaymentRequest {
  apartmentId: string
  paymentDate: string
  amount: number
  description?: string | null
  receivedBy?: string | null
  /** Hedefli tahsilat. Boşsa en eski borçtan başlayan genel FIFO uygulanır. */
  targetDebtType?: PaymentTargetDebtType | null
  /** Faz 7.1: Hedef sakin (eski kiracı borcu için) */
  targetOwnerId?: string | null
  bankAccountId?: string | null
}

export interface UpdatePaymentRequest {
  paymentDate: string
  amount: number
  description?: string | null
  receivedBy?: string | null
  targetDebtType?: PaymentTargetDebtType | null
  bankAccountId?: string | null
}

export interface RecordPaymentResult {
  paymentId: string
  apartmentId: string
  paymentDate: string
  amount: number
  receivedBy: string
}

export interface CancelPaymentRequest {
  reason: string
}

// ── Faz 6.8: Cari Hesap (Ledger) ─────────────────────────────────────────────

export interface AllocationDetail {
  paymentId: string
  paymentDate: string
  allocatedAmount: number
  target: 'OpeningBalance' | 'Invoice' | 'Interest' | 'Aidat'
}

export type RoleType = 'Yonetici' | 'Denetci'
export type RolePaymentType = 'Oran' | 'SabitTutar'
export type RolePeriodType = 'Aylik' | 'Yillik'

export interface RoleFeeBudgetItemOption {
  id: string
  year: number
  category: string
  estimatedAmount: number
}

export interface RoleFeeSettingsDTO {
  managerPaymentType: RolePaymentType
  managerExemptionRate: number
  managerFixedAmount: number
  managerPeriodType: RolePeriodType
  auditorPaymentType: RolePaymentType
  auditorExemptionRate: number
  auditorFixedAmount: number
  auditorPeriodType: RolePeriodType
  managementExpenseBudgetItemId: string | null
  availableBudgetItems: RoleFeeBudgetItemOption[]
}

export interface RoleAssignmentListItem {
  id: string
  apartmentId: string
  personName: string
  roleType: RoleType
  startDate: string
  endDate: string | null
  exemptionRate: number
  boardDecisionDate: string | null
  isOpen: boolean
  isCancelled?: boolean
  cancelledAt?: string | null
  paymentType?: RolePaymentType
  fixedAmount?: number | null
  periodType?: RolePeriodType | null
}

export interface LedgerLine {
  type: 'OpeningBalancePrincipal' | 'OpeningBalanceInterest' | 'Invoice' | 'Aidat' | 'WaiverOffset'
  sourceId: string
  label: string
  dueDate: string
  /** Aidat dönem başlangıcı. Cari hesap tarih sütunu bunu gösterir. */
  periodStart?: string | null
  principalAmount: number
  paidAmount: number
  remainingPrincipal: number
  interestAccrued: number
  lineBalance: number
  allocations: AllocationDetail[]
  /** Faz 7.1: borç sahibi sakin (Hibrit Model) — devir bakiyesinde null */
  ownerId?: string | null
  ownerName?: string | null
  /** İptal edilmiş tahakkuk satırı mı? (Denetim izi: tutar 0'a düşürülmüştür) */
  isCancelled?: boolean
  /** İptal edilmiş satırın orijinal tahakkuk tutarı */
  originalPrincipalAmount?: number | null
}

/** Faz 7.1: borç türü bazlı özet (anapara + faiz) */
export interface LedgerSummary {
  aidatPrincipalDue: number
  aidatInterestAccrued: number
  invoicePrincipalDue: number
  invoiceInterestAccrued: number
  openingBalancePrincipalDue: number
  openingBalanceInterestDue: number
}

export interface LedgerCredit {
  paymentId: string
  paymentDate: string
  amount: number
  targetDebtType: 'OpeningBalancePrincipal' | 'Invoice' | 'OpeningBalanceInterest' | 'Aidat' | null
}

export interface LedgerResult {
  apartmentId: string
  apartmentTitle: string
  doorNumber: string
  lines: LedgerLine[]
  totalPrincipalDue: number
  totalInterestAccrued: number
  totalBalance: number
  asOfDate: string
  summary?: LedgerSummary | null
  /** Borca mahsup edilemeyen tahsilat. Bakiyeyi alacağa çeker. */
  credits?: LedgerCredit[] | null
}

// ── Faz 6.8: Nakit Akış Raporu ───────────────────────────────────────────────

export interface CashFlowLine {
  date: string
  description: string
  direction: 'Income' | 'Expense'
  amount: number
  reference: string | null
}

export interface CashFlowResult {
  dateFrom: string
  dateTo: string
  totalExpense: number
  totalIncome: number
  netBalance: number
  lines: CashFlowLine[]
}

// ── Faz 6.9: Aidat (Ortak Gider) ─────────────────────────────────────────────

export type AidatDistributionMethod = 'TarifeBazli' | 'Metrekare' | 'Esit'

export type AidatChargeType = 'StandartAidat' | 'OzelToplama'

export const AIDAT_CHARGE_TYPE_LABELS: Record<AidatChargeType, string> = {
  StandartAidat: 'Standart Aidat',
  OzelToplama: 'Özel Toplama',
}

export const AIDAT_DISTRIBUTION_LABELS: Record<AidatDistributionMethod, string> = {
  TarifeBazli: 'Tarife Bazlı',
  Metrekare: 'm² Bazlı',
  Esit: 'Eşit',
}

export interface AidatTariffItem {
  id: string
  name: string
  squareMeters: number
  amount: number
  isActive: boolean
  apartmentCount: number
}

export interface CreateAidatTariffRequest {
  name: string
  squareMeters: number
  amount: number
}

export interface AidatInvoiceListItem {
  id: string
  periodStart: string
  periodEnd: string
  dueDate: string
  distributionMethod: AidatDistributionMethod
  chargeCount: number
  totalAmount: number
  paidAmount: number
  remainingAmount: number
  /** 0–100 arası yüzde */
  collectionRatio: number
  createdOn: string
  chargeType: AidatChargeType
  title: string | null
  targetAmount: number | null
  fundSpent: number
  fundBalance: number
  isCancelled?: boolean
  cancelledAt?: string | null
  isLocked?: boolean
}

export interface CreateAidatInvoiceRequest {
  periodStart: string
  periodEnd: string
  dueDate: string
  distributionMethod: AidatDistributionMethod
  equalAmount?: number | null
  note?: string | null
  chargeType?: AidatChargeType
  title?: string | null
  targetAmount?: number | null
}

export interface AidatSkippedApartment {
  doorNumber: string
  reason: string
}

export interface CreateAidatInvoiceResult {
  aidatInvoiceId: string
  periodStart: string
  periodEnd: string
  dueDate: string
  distributionMethod: AidatDistributionMethod
  chargeCount: number
  skippedCount: number
  totalAmount: number
  skippedApartments: AidatSkippedApartment[]
  chargedApartmentIds: string[]
  canNotify: boolean
  notifyBlockReason: string | null
}

export interface AidatPreviewRow {
  doorNumber: string
  apartmentTitle: string
  tariffName: string | null
  squareMeters: number | null
  amount: number
  skipReason: string | null
}

export interface AidatPreviewResult {
  chargeCount: number
  skippedCount: number
  totalAmount: number
  rows: AidatPreviewRow[]
}

export interface SetApartmentTariffRequest {
  tariffId: string | null
}

export interface SetApartmentTariffResult {
  apartmentId: string
  tariffId: string | null
  tariffName: string | null
}

export interface AssignTariffBySquareMetersResult {
  assignedCount: number
  alreadyAssignedCount: number
  noMatchCount: number
  doorNumbers: string[]
}

// ── Faz 7.1: Sakinler (Ev Sahibi + Kiracı) ───────────────────────────────────

export interface ResidentInfoInput {
  name: string
  phoneNumber?: string | null
  email?: string | null
  identityNumber?: string | null
  notes?: string | null
  leaseStartDate?: string | null
  leaseEndDate?: string | null
  validFrom?: string | null
  validTo?: string | null
}

export interface UpdateResidentsRequest {
  validFrom: string
  evSahibi?: ResidentInfoInput | null
  kiraci?: ResidentInfoInput | null
}

export interface UpdateResidentsResult {
  apartmentId: string
  evSahibiName: string | null
  kiraciName: string | null
  evSahibiId: string | null
  kiraciId: string | null
}

export type ResidentDocumentType = 'KiraSozlesmesi' | 'Tapu' | 'Diger'

export interface ResidentDocumentItem {
  id: string
  apartmentOwnerId: string
  ownerName: string
  residentType: 'EvSahibi' | 'Kiraci'
  documentType: ResidentDocumentType
  fileName: string
  uploadedOn: string
}

export type ResidentType = 'EvSahibi' | 'Kiraci'

export interface ResidentHistoryItem {
  id: string
  apartmentId: string
  doorNumber: string
  ownerName: string
  residentType: ResidentType
  validFrom: string
  validTo: string | null
  phoneNumber: string | null
  leaseStartDate: string | null
  leaseEndDate: string | null
}

export interface FormerResidentDebtItem {
  apartmentId: string
  doorNumber: string
  apartmentTitle: string
  ownerId: string
  ownerName: string
  residentTypeLabel: string
  phoneNumber: string | null
  leftOn: string | null
  principalDue: number
  interestAccrued: number
  balance: number
  evSahibiName: string | null
  rucuPossible: boolean
}

// -- Faz 6.10: Bütçe ve Gider Takibi --

export interface BudgetSettingsDTO {
  isBudgetTrackingEnabled: boolean
}

export interface BudgetRealizationRow {
  budgetItemId: string
  year: number
  category: string
  estimatedAmount: number
  actualAmount: number
  remainingAmount: number
  utilizationPercent: number
  transferIn: number
  transferOut: number
}

export interface BudgetRealizationReport {
  year: number
  buildingName: string
  totalEstimated: number
  totalActual: number
  totalRemaining: number
  utilizationPercent: number
  rows: BudgetRealizationRow[]
  totalEstimatedIncome: number
  balance: number
  isLocked: boolean
}

export interface IncomeItem {
  id: string
  year: number
  category: string
  estimatedAmount: number
}

export interface BudgetPeriodStatus {
  year: number
  isLocked: boolean
  lockedOn: string | null
}

export interface BudgetTransferItem {
  id: string
  transferDate: string
  amount: number
  note: string | null
  fromBudgetItemId: string
  toBudgetItemId: string
  fromCategory: string
  toCategory: string
}

export interface ExpenseListItem {
  id: string
  expenseDate: string
  amount: number
  description: string | null
  displayType: string
  budgetItemId: string | null
  documentNumber: string | null
  fundAidatInvoiceId: string | null
  fundTitle: string | null
  bankAccountId?: string | null
  bankAccountLabel?: string | null
}

export interface BankAccountListItem {
  id: string
  bankName: string
  accountName: string
  accountNumber: string
  iban: string | null
  openingBalance: number
  openingDate: string
  moneyIn: number
  moneyOut: number
  currentBalance: number
  isActive: boolean
}

export type BankTransactionType = 'BankInterest' | 'BankFee' | 'TransferOut' | 'TransferIn'

export interface BankTransactionListItem {
  id: string
  bankAccountId: string
  bankLabel: string
  type: BankTransactionType
  amount: number
  transactionDate: string
  description: string | null
  targetBankAccountId: string | null
  targetLabel: string | null
  transferGroupId: string | null
}

export interface CreateExpenseResult {
  id: string
  budgetWarning: boolean
  utilizationPercent: number | null
  warningMessage: string | null
}

export interface AuditExpenseRow {
  category: string
  estimated: number
  expenseAmount: number
  waiverAmount: number
  actual: number
  remaining: number
}

export interface AuditIncomeRow {
  title: string
  amount: number
}

export interface AuditBankRow {
  bankAccountId: string
  label: string
  opening: number
  moneyIn: number
  moneyOut: number
  closing: number
}

export interface AuditIncomeExpenseReport {
  year: number
  buildingName: string
  expenses: AuditExpenseRow[]
  totalEstimated: number
  totalActual: number
  totalRemaining: number
  income: AuditIncomeRow[]
  totalIncome: number
  net: number
  banks: AuditBankRow[]
  from?: string | null
  to?: string | null
}

export interface ManagementPeriodSettingsDTO {
  startDate: string
  endDate: string
}

export interface GeneralAssemblyMonthColumn {
  key: string
  year: number
  month: number
  label: string
}

export interface GeneralAssemblyMonthlySummaryRow {
  rowTitle: string
  monthValues: Record<string, number>
  total: number
}

export interface GeneralAssemblyApartmentMatrixRow {
  apartmentId: string
  doorNumber: string
  title: string
  openingBalance: number
  monthlyAccruals: Record<string, number>
  monthlyCollections: Record<string, number>
  totalAccrual: number
  totalCollected: number
  totalWaiver: number
  totalInterest: number
  closingBalance: number
}

export interface GeneralAssemblyBoardMember {
  roleTitle: string
  personName: string
  doorNumber: string
  startDate: string
  endDate: string | null
}

export interface GeneralAssemblyReport {
  from: string
  to: string
  buildingName: string
  companyName: string
  totalIncome: number
  totalCashExpense: number
  totalWaiverExpense: number
  totalActualExpense: number
  totalEstimatedExpense: number
  totalRemainingBudget: number
  netIncomeExpense: number
  totalReceivables: number
  totalResidentCredits: number
  totalBankOpeningBalance: number
  totalBankClosingBalance: number
  reconciled: boolean
  incomeRows: AuditIncomeRow[]
  expenseRows: AuditExpenseRow[]
  banks: AuditBankRow[]
  apartmentRows: ApartmentTrackingRow[]
  apartmentTotals: ApartmentTrackingRow
  monthColumns: GeneralAssemblyMonthColumn[]
  monthlySummaryRows: GeneralAssemblyMonthlySummaryRow[]
  apartmentMatrixRows: GeneralAssemblyApartmentMatrixRow[]
  boardMembers: GeneralAssemblyBoardMember[]
}

export interface ApartmentTrackingRow {
  apartmentId: string
  doorNumber: string
  title: string
  devir: number
  extraAidat: number
  aidatAccrual: number
  fuelAccrual: number
  aidatCollected: number
  fuelCollected: number
  waiver: number
  interest: number
  net: number
  ledgerBalance: number
}

export interface ApartmentTrackingReport {
  from: string
  to: string
  buildingName: string
  rows: ApartmentTrackingRow[]
  totals: ApartmentTrackingRow
  reconciled: boolean
  periodOpening: number
  openingEntered: number
  openingCollected: number
  unallocated: number
}

export interface FuelBreakdownRow {
  gasInvoiceId: string
  invoiceNumber: string
  periodFrom: string
  periodTo: string
  doorNumber: string
  title: string
  squareMeters: number
  consumptionKWh: number
  fixedAmount: number
  consumptionAmount: number
  totalAmount: number
}

export interface FuelBreakdownReport {
  from: string
  to: string
  gasInvoiceId: string | null
  total: number
  apartmentTrackingFuel: number
  reconciled: boolean
  rows: FuelBreakdownRow[]
}

export interface BankMovementRow {
  date: string
  kind: string
  description: string
  moneyIn: number
  moneyOut: number
}

export interface BankReconciliationReport {
  bankAccountId: string
  label: string
  from: string
  to: string
  opening: number
  moneyIn: number
  moneyOut: number
  closing: number
  movements: BankMovementRow[]
}


// ── Akıllı Banka Ekstresi İçe Aktarma ve Otomatik Eşleştirme ─────────────────

export type BankStatementRowStatus =
  | 'Ready'
  | 'UnmatchedApartment'
  | 'Duplicate'
  | 'InvalidRow'

export interface BankStatementPreviewDto {
  rowNumber: number
  paymentDate: string | null
  description: string
  documentNumber: string | null
  amount: number
  matchedApartmentId: string | null
  matchedDoorNumber: string | null
  matchedResidentId: string | null
  matchedResidentName: string | null
  matchedResidentType: string | null
  suggestedDebtType: PaymentTargetDebtType | null
  status: BankStatementRowStatus
  warningMessage: string | null
}

export interface BankStatementParseResult {
  rows: BankStatementPreviewDto[]
  fileErrors: string[]
  totalRows: number
  readyCount: number
  unmatchedCount: number
  duplicateCount: number
  invalidCount: number
}

export interface CreateBulkPaymentItemDto {
  apartmentId: string
  paymentDate: string
  amount: number
  description: string | null
  documentNumber: string | null
  targetDebtType: PaymentTargetDebtType | null
}

export interface CreateBulkPaymentRequest {
  bankAccountId: string | null
  receivedBy: string | null
  items: CreateBulkPaymentItemDto[]
}

export interface CreateBulkPaymentResult {
  createdCount: number
  skippedDuplicateCount: number
  totalCreatedAmount: number
  createdPaymentIds: string[]
}

export type NotificationChannel = 'Email' | 'Sms' | 'WhatsApp'
export type NotificationDeliveryStatus = 'Sent' | 'Failed' | 'Simulated'
export type NotificationPurpose = 'General' | 'Aidat' | 'Heating' | 'AllDebtors'

export interface NotificationSettingsDTO {
  testMode: boolean
  publicBaseUrl: string | null
  smtpHost: string | null
  smtpPort: number
  smtpUser: string | null
  smtpPasswordSet: boolean
  smsProvider: string
  smsUser: string | null
  smsPasswordSet: boolean
  smsHeader: string | null
  whatsAppTokenSet: boolean
  whatsAppPhoneNumberId: string | null
}

export interface NotificationSettingsUpdate {
  testMode: boolean
  publicBaseUrl?: string | null
  smtpHost?: string | null
  smtpPort: number
  smtpUser?: string | null
  smtpPassword?: string | null
  smsProvider?: string | null
  smsUser?: string | null
  smsPassword?: string | null
  smsHeader?: string | null
  whatsAppToken?: string | null
  whatsAppPhoneNumberId?: string | null
}

export interface NotificationTemplateItem {
  id: string
  name: string
  channel: NotificationChannel
  subject: string | null
  body: string
}

export interface NotificationLogItem {
  id: string
  apartmentId: string
  doorNumber: string
  channel: NotificationChannel
  recipientAddress: string
  message: string
  sentAt: string
  status: NotificationDeliveryStatus
  errorMessage: string | null
}

export interface NotificationPreview {
  recipientName: string
  recipientAddress: string | null
  subject: string | null
  body: string
  hasRecipient: boolean
}

export interface SendManualNotificationResult {
  sent: number
  failed: number
  simulated: number
  purpose: NotificationPurpose
  logs: NotificationLogItem[]
}

export interface NotificationPlaceholderItem {
  token: string
  description: string
  example: string
}
