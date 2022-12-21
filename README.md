# SaaS Lifecycle Connector for GitHub

🧪⚠️ __Highly experimental.__ Don't use in production.

## Overview

The SaaS Lifecycle Connector for GitHub (SLCG) is an experimental accelerator designed to make it easier for SaaS ISVs to manage the lifecycle of subscription-supported cloud resources — from provisioning to billing events to eventual cancellation — in a lightweight, unopinionated, and very cost-efficient way.

SLCG's design is based on the premise that SaaS ISVs should be spending the bulk of their cloud budget on resources that can be tied directly back to customer subscriptions. Resources are provisioned and scaled just-in-time and deprovisioned automatically when no longer needed. SLCG is self-healing and very cost-effective to run in your own Azure enviroment (typically costing < $10 USD/mo. to operate) highlighting the goal of making SaaS less financially-risky to build while allowing ISVs to make powerful subscription-supported architectural choices that might have been cost-prohibitive in the past.

## How it works


