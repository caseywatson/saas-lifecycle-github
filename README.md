# Edgar

| E | D | G | A | R |
| --- | --- | --- | --- | --- |
| Event | Driven | GitHub | Action | Runner |

> 🧪⚠️ __Highly experimental.__ Don't use in production. You've been warned.

## Edgar and SaaS apps

Edgar is designed primarily to support ISVs that are building SaaS apps in the cloud.

Customers purchase subscriptions (e.g., annual, monthly, etc.) to SaaS apps published by independent software vendors (or ISVs). Financially, it's critical that ISVs respond in as near real time as possible to subscription lifecycle events (e.g., purchases, suspensions, reinstatements, cancelations, etc.) to reconfigure the SaaS app's supporting cloud infrastructure. The idea is to continuously keep ISV revenue (what the ISV's customers are paying) and cloud spend in close alignment. When possible (depending on app tenancy model), ISVs should only be paying for cloud resources that their customers are paying for through their subscriptions.

Edgar leverages GitHub Actions to tighten the lead time between subscription-related events and cloud infrastructure configuration enabling customers to access their SaaS subscriptions faster and ISVs to reduce idle resources and optimize cloud spend.

### An example

Let's say for a moment that I'm building a SaaS app. Each subscription that a customer purchases is powered by a set of dedicated (single-tenant) Azure virtual machines (or VMs). Let's walk through the lifecycle of the subscription together to understand why it's so important to automate the subscription lifecycle process.

* When a customer purchases a SaaS subscription, a dedicated set of VMs should be created.
* When a subscription is suspended due to non-payment, the VM is stopped/deallocated. In this state, the VMs still exist but are no longer accessible. Most importantly, the ISV is no longer billed for its usage (apart from disk storage).
* When a subscription is canceled, the VM is deleted. The ISV is no longer paying for compute or disk storage to support the subscription.

While this is a very simple example, the core concept should be apparent. VMs are created only when customers are paying for them. If, for some reason, the customer's payment instrument becomes invalid, the subscription is moved into a suspended state and the the VMs are stopped/deallocated so that the ISV is no longer paying for them (because the customer isn't). When a subscription has been fully canceled, it's safe to delete the VMs themselves.
