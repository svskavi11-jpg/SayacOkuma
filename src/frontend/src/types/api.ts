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