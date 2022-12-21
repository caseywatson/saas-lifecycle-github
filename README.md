# SaaS Lifecycle Connector for GitHub

🧪⚠️ __Highly experimental.__ Don't use in production.

## Overview

The SaaS Lifecycle Connector for GitHub (SLCG) is an experimental accelerator designed to make it easier for SaaS ISVs to manage the lifecycle of subscription-supported cloud resources — from provisioning to billing events to eventual cancellation — in a lightweight, unopinionated, and very cost-efficient way.

SLCG's design is based on the premise that SaaS ISVs should be spending the bulk of their cloud budget on resources that can be tied directly back to customer subscriptions. Resources are provisioned and scaled just-in-time and deprovisioned automatically when no longer needed. SLCG is self-healing and very cost-effective to run in your own Azure enviroment (typically costing < $10 USD/mo. to operate) highlighting the goal of making SaaS less financially-risky to build while allowing ISVs to make powerful subscription-supported architectural choices that might have been cost-prohibitive in the past.

## How it works

![SLCG Functions](slcg.png)

| Function | Notes |
| --- | --- | 
| __Dispatch__ | [The Dispatch function](https://github.com/caseywatson/saas-lifecycle-github/blob/main/Edgar/Dispatch.cs) first [checks the repo map (❶) to see if there is a GitHub repo available to service the request](https://github.com/caseywatson/saas-lifecycle-github/blob/02761146764a98123d35bfb560f33339f9c2de09/Edgar/Dispatch.cs#L73). Then, it [stages the operation blob (❷) for tracking](https://github.com/caseywatson/saas-lifecycle-github/blob/02761146764a98123d35bfb560f33339f9c2de09/Edgar/Dispatch.cs#L130) and [invokes the appropriate GitHub Action (❸)](https://github.com/caseywatson/saas-lifecycle-github/blob/02761146764a98123d35bfb560f33339f9c2de09/Edgar/Dispatch.cs#L138). Finally, [it publishes a “Configuring” event (❾)](https://github.com/caseywatson/saas-lifecycle-github/blob/02761146764a98123d35bfb560f33339f9c2de09/Edgar/Dispatch.cs#L139). |
| __Refresh__ | [The Refresh function](https://github.com/caseywatson/saas-lifecycle-github/blob/main/Edgar/Refresh.cs) periodically [takes an inventory of all accessible GitHub repos available to handle SaaS lifecycle operations (❹)](https://github.com/caseywatson/saas-lifecycle-github/blob/db0e79c2f1a4d71af77f743197d391ed68b058eb/Edgar/Refresh.cs#L48) and, if required, [updates the repo map accordingly (❺)](https://github.com/caseywatson/saas-lifecycle-github/blob/db0e79c2f1a4d71af77f743197d391ed68b058eb/Edgar/Refresh.cs#L107). |
| __Reconcile__ | [The Reconcile function](https://github.com/caseywatson/saas-lifecycle-github/blob/main/Edgar/Reconcile.cs) periodically attempts to [reconcile outstanding operations (❻)](https://github.com/caseywatson/saas-lifecycle-github/blob/db0e79c2f1a4d71af77f743197d391ed68b058eb/Edgar/Reconcile.cs#L41) with [completed GitHub Action runs (❼)](https://github.com/caseywatson/saas-lifecycle-github/blob/db0e79c2f1a4d71af77f743197d391ed68b058eb/Edgar/Reconcile.cs#L68). Once reconciled, [the applicable operation blob is deleted](https://github.com/caseywatson/saas-lifecycle-github/blob/db0e79c2f1a4d71af77f743197d391ed68b058eb/Edgar/Reconcile.cs#L100), and the function [publishes either a “Configuration Succeeded [or] Failed” event (❾)](https://github.com/caseywatson/saas-lifecycle-github/blob/db0e79c2f1a4d71af77f743197d391ed68b058eb/Edgar/Reconcile.cs#L107). |
| __Expire__ | The Expire function provides a self-healing capability that [periodically checks for staged operation blobs that are more than 30 days old (❽)](https://github.com/caseywatson/saas-lifecycle-github/blob/19e8d7a3fc2104bd77dcdb0e9dd46acdea1f3fce/Edgar/Expire.cs#L38). For each expired operation, the function [deletes the operation blob](https://github.com/caseywatson/saas-lifecycle-github/blob/19e8d7a3fc2104bd77dcdb0e9dd46acdea1f3fce/Edgar/Expire.cs#L58) and [publishes a “Configuration Timed Out” event (❾)](https://github.com/caseywatson/saas-lifecycle-github/blob/19e8d7a3fc2104bd77dcdb0e9dd46acdea1f3fce/Edgar/Expire.cs#L59). |





