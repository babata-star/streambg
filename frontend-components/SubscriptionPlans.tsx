import { useState, useEffect } from 'react'
import api from '../api/client'
import styles from './SubscriptionPlans.module.css'

interface Plan {
  id: number
  name: string
  description?: string
  priceMonthly: number
  currencyCode: string
  badgeEmoji?: string
  badgeColor?: string
  perks: string[]
}

interface SubscriptionState {
  isSubscribed: boolean
  badge?: { emoji?: string; color?: string; totalMonths: number } | null
}

interface Props {
  creatorUsername: string
  onSubscribed?: () => void
}

export function SubscriptionPlans({ creatorUsername, onSubscribed }: Props) {
  const [plans, setPlans] = useState<Plan[]>([])
  const [subState, setSubState] = useState<SubscriptionState>({ isSubscribed: false })
  const [loading, setLoading] = useState(true)
  const [subscribing, setSubscribing] = useState<number | null>(null)

  useEffect(() => {
    Promise.all([
      api.get(`/subscriptions/plans/${creatorUsername}`).then(r => setPlans(r.data)),
      api.get(`/subscriptions/check/${creatorUsername}`).then(r => setSubState(r.data)).catch(() => {})
    ]).finally(() => setLoading(false))
  }, [creatorUsername])

  const handleSubscribe = async (planId: number) => {
    setSubscribing(planId)
    try {
      await api.post(`/subscriptions/subscribe/${planId}`)
      setSubState({ isSubscribed: true })
      onSubscribed?.()
    } catch (err: any) {
      alert(err?.response?.data?.error ?? 'Грешка при абонамент')
    } finally {
      setSubscribing(null)
    }
  }

  if (loading) return <div className={styles.loading}>Зарежда се...</div>
  if (plans.length === 0) return null

  return (
    <div className={styles.wrapper}>
      <h3 className={styles.heading}>Абонирай се</h3>

      {subState.isSubscribed && subState.badge && (
        <div className={styles.activeBadge}>
          <span>{subState.badge.emoji} Активен абонат</span>
          <span className={styles.months}>{subState.badge.totalMonths} мес.</span>
        </div>
      )}

      <div className={styles.plans}>
        {plans.map((plan, i) => (
          <div key={plan.id} className={`${styles.plan} ${i === 1 ? styles.featured : ''}`}>
            {i === 1 && <div className={styles.popularBadge}>Популярен</div>}

            <div className={styles.planHeader}>
              <span className={styles.planEmoji}>{plan.badgeEmoji ?? '⭐'}</span>
              <div>
                <h4 className={styles.planName}>{plan.name}</h4>
                {plan.description && (
                  <p className={styles.planDesc}>{plan.description}</p>
                )}
              </div>
            </div>

            <div className={styles.price}>
              <span className={styles.priceNum}>{plan.priceMonthly.toFixed(2)}</span>
              <span className={styles.pricePer}>лв./мес</span>
            </div>

            <ul className={styles.perks}>
              {plan.perks.map((perk, j) => (
                <li key={j}>
                  <span className={styles.perkCheck}>✓</span>
                  {perk}
                </li>
              ))}
            </ul>

            <button
              className={`${styles.subBtn} ${subState.isSubscribed ? styles.subActive : ''}`}
              onClick={() => handleSubscribe(plan.id)}
              disabled={subState.isSubscribed || subscribing === plan.id}
            >
              {subscribing === plan.id ? 'Обработва се...'
                : subState.isSubscribed ? '✓ Абониран'
                : 'Абонирай се'}
            </button>
          </div>
        ))}
      </div>
    </div>
  )
}
