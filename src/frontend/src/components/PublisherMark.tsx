type PublisherTone = 'dark' | 'light'

export function PublisherMark({ tone, flush = false }: { tone: PublisherTone; flush?: boolean }) {
  const title = tone === 'dark' ? 'rgba(255,255,255,0.92)' : '#0f172a'
  const phone = tone === 'dark' ? 'rgba(255,255,255,0.65)' : '#475569'
  const line = tone === 'dark' ? 'rgba(255,255,255,0.16)' : 'rgba(15,23,42,0.08)'

  return (
    <div
      style={{
        display: 'flex',
        gap: 8,
        alignItems: 'center',
        padding: flush ? '16px 0 0' : '10px 12px 12px',
        borderTop: `1px solid ${line}`,
        flex: '0 0 auto',
      }}
    >
      <img
        src="/brand/simsek-logo.png"
        alt=""
        width={32}
        height={32}
        style={{ borderRadius: 6, flex: '0 0 auto' }}
      />
      <div style={{ minWidth: 0 }}>
        <a
          href="https://simsek.gen.tr"
          target="_blank"
          rel="noreferrer"
          style={{ color: title, fontSize: 12, fontWeight: 650, lineHeight: 1.3, display: 'block' }}
        >
          Şimşek Bilgisayar Ltd. Şti.
        </a>
        <a href="tel:+908503057665" style={{ color: phone, fontSize: 11, display: 'block', lineHeight: 1.4 }}>
          0 850 305 7665
        </a>
        <a href="tel:+904422132050" style={{ color: phone, fontSize: 11, display: 'block', lineHeight: 1.4 }}>
          0 442 213 2050
        </a>
      </div>
    </div>
  )
}
