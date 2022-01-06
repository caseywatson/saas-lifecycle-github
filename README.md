# Edgar
[E]vent-[D]riven [G]itHub [A]ction [R]unner

> 🧪⚠️ __Highly experimental.__ Don't use in production. You've been warned.

## Edgar and SaaS apps

Edgar is designed primarily to support ISVs that are building SaaS apps on the Microsoft Azure cloud platform.

Customers purchase subscriptions (e.g., annual, monthly, etc.) to SaaS apps published by an independent software vendor (or ISV). Financially, it's critical that ISVs respond in as near real time as possible to subscription lifecycle events (e.g., purchases, suspensions, reinstatements, cancelations, etc.) to reconfigure the SaaS app's supporting cloud infrastructure keeping revenue (what the ISV's customers are paying) and ISV cloud spend in close alignment. To put it simply, when possible (depending on app tenancy models), ISVs should only pay for cloud resources that their customers are currently paying for through their subscriptions. Where possible (depending on tenancy model), cloud resources should be provisioned when a customer starts paying for them and deleted when the cusxtomer stops paying. By tightening the lead time between subscription-related events and cloud infrastructure configuration, customers can access their subscriptions faster while ISVs can remove idle resources and optimize their cloud spend.
