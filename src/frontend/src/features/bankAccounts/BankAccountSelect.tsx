import type { CSSProperties } from "react"
import { useQuery } from "@tanstack/react-query"
import { Select } from "antd"
import { apiClient } from "../../api/client"
import type { BankAccountListItem } from "../../types/api"

export function BankAccountSelect({
  value,
  onChange,
  includeId,
  excludeId,
  allowClear = true,
  placeholder = "Banka hesabı (isteğe bağlı)",
  style,
}: {
  value?: string | null
  onChange?: (value: string | null) => void
  includeId?: string | null
  excludeId?: string | null
  allowClear?: boolean
  placeholder?: string
  style?: CSSProperties
}) {
  const accountsQuery = useQuery({
    queryKey: ["bank-accounts"],
    queryFn: async () => (await apiClient.get<BankAccountListItem[]>("/bank-accounts")).data,
  })

  const options = (accountsQuery.data ?? [])
    .filter((account) => account.id !== excludeId)
    .filter((account) => account.isActive || account.id === includeId || account.id === value)
    .map((account) => ({
      value: account.id,
      label: `${account.bankName} — ${account.accountName} (${account.accountNumber})`,
    }))

  return (
    <Select
      allowClear={allowClear}
      showSearch
      optionFilterProp="label"
      placeholder={placeholder}
      loading={accountsQuery.isLoading}
      options={options}
      value={value ?? undefined}
      onChange={(next) => onChange?.(next ?? null)}
      style={style ?? { minWidth: 280 }}
    />
  )
}