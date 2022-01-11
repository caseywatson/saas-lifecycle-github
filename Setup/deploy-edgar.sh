#!/bin/bash

SECONDS=0 # Let's time it...

usage() { echo "Usage: $0 <-n name> <-p github_pat> <-r deployment_region> [-o github_repo_owner]"; }

check_az() {
    az version >/dev/null

    if [[ $? -ne 0 ]]; then
        echo "❌   Please install the Azure CLI before continuing. See [https://docs.microsoft.com/cli/azure/install-azure-cli] for more information."
        return 1
    else
        echo "✔   Azure CLI installed."
    fi
}

check_dotnet() {
    dotnet --version >/dev/null

    if [[ $? -ne 0 ]]; then
        echo "❌   Please install .NET before continuing. See [https://dotnet.microsoft.com/download] for more information."
        return 1
    else
        echo "✔   .NET installed."
    fi
}

check_deployment_region() {
    region=$1

    region_display_name=$(az account list-locations -o tsv --query "[?name=='$region'].displayName")

    if [[ -z $region_display_name ]]; then
        echo "❌   [$region] is not a valid Azure region. For a full list of Azure regions, run 'az account list-locations -o table'."
        return 1
    else
        echo "✔   [$region] is a valid Azure region ($region_display_name)."
    fi
}

check_deployment_name() {
    name=$1

    if [[ $name =~ ^[a-z0-9]{5,13}$ ]]; then
        echo "✔   [$name] is a valid Edgar deployment name."
    else
        echo "❌   [$name] is not a valid Edgar deployment name. The name must contain only lowercase letters and numbers and be between 5 and 13 characters in length."
        return 1
    fi
}

splash() {
    echo "E | vent"
    echo "D | riven"
    echo "G | itHub"
    echo "A | ction"
    echo "R | unner"
    echo
    echo "Edgar | 0.1-experimental"
    echo "https://github.com/caseywatson/edgar"
    echo
    echo "🧪   ⚠️   Highly experimental. Don't use in production."
    echo
}

# Introductions...

splash

# Make sure all pre-reqs are installed...

echo "Checking setup prerequisites...";

check_az;           [[ $? -ne 0 ]] && prereq_check_failed=1
check_dotnet;       [[ $? -ne 0 ]] && prereq_check_failed=1

if [[ -z $prereq_check_failed ]]; then
    echo "✔   All setup prerequisites installed."
else
    echo "❌   Please install all setup prerequisites then try again."
    return 1
fi

# Get our parameters...

p_repo_owner_name=""

while getopts "n:o:p:r:" opt; do
    case $opt in
        n)
            p_deployment_name=$OPTARG
        ;;
        o)
            p_repo_owner_name=$OPTARG
        ;;
        p)
            p_github_pat=$OPTARG
        ;;
        r)
            p_deployment_region=$OPTARG
        ;;
        \?)
            usage
            exit 1
        ;;
    esac
done

# Check our parameters...

echo "Validating script parameters..."

[[ -z p_deployment_name || -z p_github_pat || -z p_deployment_region ]] && { usage; exit 1; }

check_deployment_region $p_deployment_region;   [[ $? -ne 0 ]] && param_check_failed=1
check_deployment_name $p_deployment_name;       [[ $? -ne 0 ]] && param_check_failed=1

if [[ -z $param_check_failed ]]; then
    echo "✔   All setup parameters are valid."
else
    echo "❌   Parameter validation failed. Please review and try again."
    return 1
fi

# Create our resource group if it doesn't already exist...

resource_group_name="edgar-$p_deployment_name"

if [[ $(az group exists --resource-group "$resource_group_name" --output tsv) == false ]]; then
    echo "Creating resource group [$resource_group_name]..."

    az group create --location "$p_deployment_region" --name "$resource_group_name"

    if [[ $? -eq 0 ]]; then
        echo "✔   Resource group [$resource_group_name] created."
    else
        echo "❌   Unable to create resource group [$resource_group_name]."
        exit 1
    fi
fi

# Deploying the ARM template...

subscription_id=$(az account show --query id --output tsv)
arm_deployment_name="edgar-deploy-$p_deployment_name"

echo "🏗️    Deploying ARM template to subscription [$subscription_id] resource group [$resource_group_name]..."

az deployment group create \
    --resource-group "$resource_group_name" \
    --name "$arm_deployment_name" \
    --template-file "./deploy-edgar.json" \
    --parameters \
        pat="$p_github_pat" \
        repo_owner="$p_repo_owner_name" \
        name="$p_deployment_name"

function_app_name=$(az deployment group show --resource-group "$resource_group_name" --name "$arm_deployment_name" --query properties.outputs.functionAppName.value --output tsv);

# Package up the function app for deployment...

echo "📦    Packaging function app [$function_app_name] for deployment..."

dotnet publish -c Release -o ./topublish ../Edgar/Edgar.Functions.csproj

cd ./topublish
zip -r ../topublish.zip . >/dev/null
cd ..

# Push the deployment package up to the function app...

echo "☁️    Publishing function app [$function_app_name]..."

az functionapp deployment source config-zip \
    --resource-group "$resource_group_name" \
    --name "$function_app_name" \
    --src "./topublish.zip"

# Instead of querying for candidate repos each time an operation is dispatched, Edgar maintains a map of all 
# candidate repos it can access in blob storage. The [Refresh] function runs every five minutes by default and, if needed (based on etag),
# will update the repo map. This process is eventually consistent as we don't expect the list of candidate repos
# to change all that often. If the repo map is unavailable (it hasn't been created yet), the dispatch API will return a
# 503 (Service Unavailable). To make sure that Edgar is ready to go as soon as this deployment script is done running,
# we "prime" the repo map here by manually running the [Refresh] function as part of setup. Otherwise, it's possible
# that Edgar will return 503s for the first five minutes after setup.
 
echo "🗺️    Building initial repo map (repo_map.json)..."

master_key_url="https://management.azure.com/subscriptions/$subscription_id/resourceGroups/$resource_group_name/providers/Microsoft.Web/sites/$function_app_name/host/default/listKeys?api-version=2018-11-01"
master_key=$(az rest --method post --uri "$master_key_url" --query masterKey --output tsv)

curl -v -X POST \
    -H "Content-Type: application/json" \
    -H "x-functions-key: $master_key" \
    -d "{ \"input\": \"test\" }" \
    "https://$function_app_name.azurewebsites.net/admin/functions/Refresh"

# Leave only footsteps...

echo "🧹   Cleaning up..."

rm -rf ./topublish >/dev/null
rm -rf ./topublish.zip >/dev/null

echo
echo "Edgar Deployment Summary"
echo
echo "Name ........................ [$p_deployment_name]"
echo "Azure Subscription ID ....... [$subscription_id]"
echo "Azure Resource Group ........ [$resource_group_name]"
echo "Dispatch Endpoint URL ....... [https://$function_app_name.azurewebsites.net/api/saas/{tenantId}/subscriptions/{subscriptionId}/{actionType}?code=$master_key]"
echo
echo "Setup took [$SECONDS] seconds."
echo
echo "Learn more about Edgar at [ https://github.com/caseywatson/edgar ]."








