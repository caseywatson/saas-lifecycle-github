# Edgar
[E]vent-[D]riven [G]itHub [A]ction [R]unner

> 🧪⚠️ __Highly experimental.__ Don't use in production. You've been warned.

## Edgar and SaaS apps

Edgar is designed primarily to support ISVs that are building SaaS apps in the cloud.

Customers purchase subscriptions (e.g., annual, monthly, etc.) to SaaS apps published by an independent software vendor (or ISV). Financially, it's critical that ISVs respond in as near real time as possible to subscription lifecycle events (e.g., purchases, suspensions, reinstatements, cancelations, etc.) to reconfigure the SaaS app's supporting cloud infrastructure keeping revenue (what the ISV's customers are paying) and ISV cloud spend continuously in close alignment. To put it simply, when possible (depending on app tenancy models), ISVs should only pay for cloud resources that their customers are currently paying for through their subscriptions.

Edgar leverages GitHub Actions to tighten the lead time between subscription-related events and cloud infrastructure configuration enabling customers to access their SaaS subscriptions faster and ISVs to reduce idle resources and optimize cloud spend.
