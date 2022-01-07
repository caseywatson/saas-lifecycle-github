#!/bin/bash

exec 3>&2 # Grabbing a reliable stderr handle...

usage() { printf "\nUsage: $0 <-p github-pat> <-r deployment-region> [-n deployment-name]\n"; }

check_az() {
    exec 3>&2

    az version >/dev/null 2>&1

    if [[ $? -ne 0 ]]; then
        echo "❌   Please install the Azure CLI before continuing. See [https://docs.microsoft.com/cli/azure/install-azure-cli] for more information."
        return 1
    else
        echo "✔   Azure CLI installed."
    fi
}

check_dotnet() {
    exec 3>&2

    dotnet --version >/dev/null 2>&1

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

check_prereqs() {
    echo "Checking Edgar setup prerequisites...";

    check_az;         if [[ $? -ne 0 ]]; then prereq_check_failed=1; fi;
    check_dotnet;     if [[ $? -ne 0 ]]; then prereq_check_failed=1; fi;

    if [[ -z $prereq_check_failed ]]; then
        echo "✔   All Edgar setup prerequisites installed."
    else
        return 1
    fi
}

while getopts "r:n:p:" opt; do
    case $opt in
        r)
            deployment_region=$OPTARG
        ;;
        n)
            deployment_name=$OPTARG
        ;;
        p)
            github_pat=$OPTARG
        ;;
        \?)
            usage
            exit 1
        ;;
    esac
done

# Check for missing parameters.

[[ -z $deployment_region || -z $github_pat ]] && { usage; exit 1; }

# Set deployment and resource group names.

[[ -z $deployment_name ]] && deployment_name=`date +%N`
resource_group_name="edgar-$deployment_name"

# Is the deployment name valid?

check_deployment_name "$deployment_name"

[[ $? -ne 0 ]] && exit 1;

# Are all of our pre-reqs satisfied?

check_prereqs

if [[ $? -ne 0 ]]; then
    echo "❌   Please install all Edgar setup prerequisites then try again. Setup failed."
    exit 1
fi

# Is the specified deployment region valid?

check_deployment_region "$deployment_region";

[[ $? -ne 0 ]] && exit 1;

# Create the resource group if it doesn't already exist.

if [[ $(az group exists --resource-group "$resource_group_name" --output tsv) == false ]]; then
    echo "Creating resource group [$resource_group_name]..."
    az group create --location "$deployment_region" --name "$resource_group_name"

    if [[ $? -eq 0 ]]; then
        echo "✔   Resource group [$resource_group_name] created."
    else
        echo "❌   Unable to create resource group [$resource_group_name]. See above output for details. Setup failed."
        exit 1
    fi
elif [[ -n $(az resource list --resource-group "$resource_group_name" --output tsv) ]]; then
    echo "$❌   Edgar must be deployed into an empty resource group. Resource group [$resource_group_name] contains resources. Setup failed."
    exit 1
fi

subscription_id=$(az account show --query id --output tsv);

# Deploy the ARM template.

echo "Deploying Edgar to subscription [$subscription_id] resource group [$resource_group_name]..."

az_deployment_name="edgar-deploy-$deployment_name"






