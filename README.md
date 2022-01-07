# Edgar

| E | D | G | A | R |
| --- | --- | --- | --- | --- |
| Event | Driven | GitHub | Action | Runner |

🧪⚠️ __Highly experimental.__ Don't use in production.

## Edgar and SaaS apps

Edgar is designed to support independent software vendors (ISVs) that are building cloud-based SaaS apps.

Customers purchase subscriptions (e.g., annual, monthly, etc.) to SaaS apps published by ISVs. Financially, it's critical that ISVs respond in as near real time as possible to subscription lifecycle events (e.g., purchases, suspensions, reinstatements, cancelations, etc.) to reconfigure the SaaS app's supporting cloud infrastructure.

The idea is to continuously keep ISV revenue (what the ISV's customers are paying) and cloud spend in close alignment. When possible, ISVs should only be paying for cloud resources that their customers are paying for through their subscriptions. 

> There are always costs associated with _enabling_ SaaS including those needed to run Edgar. These costs aren't directly supported by subscribers. ISVs should focus on minimizing the cost of _enabling_ SaaS while their subscribers pay for resources needed to _deliver_ SaaS.

Edgar leverages GitHub Actions to tighten the lead time between subscription-related events and cloud infrastructure configuration enabling customers to access their SaaS subscriptions faster and ISVs to reduce idle resources and optimize cloud spend.

### An example

Let's say you're building an Azure-based SaaS app. Each subscription that a customer purchases is powered by a set of dedicated (single-tenant) [Azure virtual machines (or VMs)](https://azure.microsoft.com/services/virtual-machines/). For our purposes, VMs are a great example because, in Azure, [they're charged by the minute based on hourly rates](https://azure.microsoft.com/pricing/details/virtual-machines/linux/) allowing you to potentially keep your revenue and cloud spend in alignment at the minute-level time grain.

Let's walk through the lifecycle of a SaaS subscription together to understand why automation is so important here.

* When a customer purchases a SaaS subscription, a dedicated set of VMs should be automatically created.
* When a subscription is suspended due to non-payment, the VMs should be [deallocated](https://docs.microsoft.com/azure/virtual-machines/states-billing#power-states-and-billing).
  * In this state, the VMs still exist but are no longer accessible. Most importantly, you are no longer being billed for their usage (apart from disk storage).
* When a subscription is canceled, the VMs should be deleted.

While this is a very simple example, the core concept should be obvious. VMs are created only when customers are paying for them. If, for some reason, the customer's payment instrument becomes invalid, the subscription is moved into a suspended state and the VMs are deallocated so that the ISV is no longer paying for them (because the customer isn't). When a subscription has been fully canceled, it's safe to delete the VMs themselves.
