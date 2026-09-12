using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Conversion;
using CRMPark.FetchOperations.Search;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Workflow.Activities;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Common
{
    public class UnshareRecord : ShareStepBase
    {
        public UnshareRecord() 
        {
            _workflowName = "UnshareRecord";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, EntityReference target, EntityReference systemUser)
        {
            // Формируем запрос на удаление доступа к записи
            RevokeAccessRequest revokeAccessRequest = new RevokeAccessRequest()
            {
                Revokee = systemUser,
                Target = target
            };
            service.Execute(revokeAccessRequest);
        }
    }
}
