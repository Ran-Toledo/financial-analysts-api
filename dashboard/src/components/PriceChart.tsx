import {
  ColorType,
  createChart,
  createSeriesMarkers,
  CrosshairMode,
  LineSeries,
  LineStyle,
  LineType,
  type IChartApi,
  type ISeriesApi,
  type ISeriesMarkersPluginApi,
  type SeriesMarker,
  type Time,
} from 'lightweight-charts'
import { useEffect, useMemo, useRef, useState } from 'react'
import type { Schemas } from '../api/client'
import { formatAction, formatDate, formatPrice } from '../lib/format'
import { ratingsByTradingDay, targetLine } from '../lib/priceChart'
import { Legend } from './Controls'
import { useCssColors } from './useCssColors'

type Rating = Schemas['RatingDto']

const TOKENS = ['text', 'text-2', 'text-3', 'grid', 'surface', 'buy', 'hold', 'sell', 'target'] as const

interface Hover {
  date: string
  x: number
  /** Chart width when hovered, to keep the tooltip inside it. */
  width: number
}

/**
 * Daily closes with a marker on each rating change (▲ upgrade, ▼ downgrade, ● initiation colored by the new
 * rating) and, when the data has price targets, a dashed step line for the month-end mean target.
 * Built on TradingView Lightweight Charts, which shows its attribution logo as its license requires.
 */
export function PriceChart({
  prices,
  changes,
  targets,
}: {
  prices: readonly Schemas['PriceDto'][]
  changes: readonly Rating[]
  targets: readonly Schemas['ConsensusPointDto'][]
}) {
  const container = useRef<HTMLDivElement>(null)
  const chart = useRef<IChartApi | null>(null)
  const priceSeries = useRef<ISeriesApi<'Line'> | null>(null)
  const targetSeries = useRef<ISeriesApi<'Line'> | null>(null)
  const markers = useRef<ISeriesMarkersPluginApi<Time> | null>(null)
  const [hover, setHover] = useState<Hover | null>(null)
  const colors = useCssColors(TOKENS)

  // Rating dates and month-end targets snapped onto trading days, so everything lands on a bar.
  const { changesByDay, targetPoints } = useMemo(() => {
    const days = prices.map((p) => p.date)
    return { changesByDay: ratingsByTradingDay(changes, days), targetPoints: targetLine(targets, days) }
  }, [prices, changes, targets])

  // Create the chart once.
  useEffect(() => {
    const el = container.current!
    const c = createChart(el, {
      autoSize: true,
      layout: { background: { type: ColorType.Solid, color: 'transparent' }, fontFamily: 'inherit', fontSize: 12 },
      grid: { vertLines: { visible: false } },
      rightPriceScale: { borderVisible: false },
      timeScale: { borderVisible: false, fixLeftEdge: true, fixRightEdge: true },
      crosshair: { mode: CrosshairMode.Magnet, horzLine: { visible: false, labelVisible: false } },
      // Leave the mouse wheel to the page.
      handleScroll: { mouseWheel: false },
      handleScale: { mouseWheel: false },
    })
    const target = c.addSeries(LineSeries, {
      lineWidth: 2,
      lineStyle: LineStyle.Dashed,
      lineType: LineType.WithSteps,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    })
    const price = c.addSeries(LineSeries, { lineWidth: 2, priceLineVisible: false, crosshairMarkerRadius: 4 })
    chart.current = c
    priceSeries.current = price
    targetSeries.current = target
    markers.current = createSeriesMarkers(price, [])

    c.subscribeCrosshairMove((param) => {
      if (!param.time || !param.point || param.point.x < 0 || param.point.y < 0) setHover(null)
      else setHover({ date: param.time as string, x: param.point.x, width: el.clientWidth })
    })
    return () => {
      c.remove()
      chart.current = null
    }
  }, [])

  // Theme colors (canvas can't read CSS variables, so they're passed in resolved).
  useEffect(() => {
    chart.current?.applyOptions({
      layout: { textColor: colors['text-3'] },
      grid: { horzLines: { color: colors.grid } },
      crosshair: { vertLine: { color: colors['text-3'], labelBackgroundColor: colors['text-2'] } },
    })
    priceSeries.current?.applyOptions({ color: colors['text-2'], crosshairMarkerBorderColor: colors.surface })
    targetSeries.current?.applyOptions({ color: colors.target })
  }, [colors])

  // Data and markers.
  useEffect(() => {
    priceSeries.current?.setData(prices.map((p) => ({ time: p.date, value: p.close })))
    targetSeries.current?.setData(targetPoints)

    const list: SeriesMarker<Time>[] = []
    for (const [day, ratings] of changesByDay) {
      for (const r of ratings) {
        if (r.action === 'Upgrade') list.push({ time: day, position: 'belowBar', shape: 'arrowUp', color: colors.buy })
        else if (r.action === 'Downgrade')
          list.push({ time: day, position: 'aboveBar', shape: 'arrowDown', color: colors.sell })
        else
          list.push({
            time: day,
            position: 'belowBar',
            shape: 'circle',
            color: colors[(r.category?.toLowerCase() ?? 'hold') as 'buy' | 'hold' | 'sell'],
            size: 0.6,
          })
      }
    }
    markers.current?.setMarkers(list)
    chart.current?.timeScale().fitContent()
  }, [prices, targetPoints, changesByDay, colors])

  const hoveredPrice = hover && prices.find((p) => p.date === hover.date)
  const hoveredTarget = hover && targetPoints.findLast((t) => t.time <= hover.date)
  const hoveredChanges = (hover && changesByDay.get(hover.date)) ?? []

  return (
    <>
      <div className="price-chart" ref={container} onMouseLeave={() => setHover(null)}>
        {hover && hoveredPrice && (
          <div
            className="chart-tip price-tip"
            style={{
              left: Math.min(Math.max(hover.x + 14, 0), Math.max(hover.width - 240, 0)),
              top: 8,
            }}
          >
            <div className="muted">{formatDate(hover.date)}</div>
            <div>
              Close <strong>{formatPrice(hoveredPrice.close)}</strong>
            </div>
            {hoveredTarget && <div>Mean target {formatPrice(hoveredTarget.value)}</div>}
            {hoveredChanges.slice(0, 4).map((r) => (
              <div key={r.id}>
                {formatAction(r.action)} by {r.analystName ?? r.firmName}
                {r.ratingTo ? ` to ${r.ratingTo}` : ''}
              </div>
            ))}
            {hoveredChanges.length > 4 && <div className="muted">and {hoveredChanges.length - 4} more</div>}
          </div>
        )}
      </div>
      <Legend
        items={[
          { label: 'Close', swatch: <span className="swatch-line swatch-price" /> },
          ...(targetPoints.length > 0
            ? [{ label: 'Mean target (month-end)', swatch: <span className="swatch-line swatch-target" /> }]
            : []),
          { label: 'Upgrade', swatch: <span className="swatch-glyph dot-Buy-text">▲</span> },
          { label: 'Downgrade', swatch: <span className="swatch-glyph dot-Sell-text">▼</span> },
          { label: 'Initiation (colored by rating)', swatch: <span className="swatch-dot dot-Hold" /> },
        ]}
      />
    </>
  )
}
