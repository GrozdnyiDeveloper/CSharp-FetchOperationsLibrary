using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMPark.FetchOperations.Base
{
    public class WorkflowActivityContext
    {
        public CodeActivityContext CodeActivityContext { get; protected set; }
        public ITracingService TracingService { get; protected set; }
        public IWorkflowContext WorkflowContext { get; protected set; }
        public IOrganizationServiceFactory ServiceFactory { get; private set; }
        public IOrganizationService GetOrganizationService(Guid? userId = null)
        {
            return ServiceFactory.CreateOrganizationService(userId);
        }

        public OrganizationServiceContext GetOrganizationServiceContext(Guid? userId = null)
        {
            return new OrganizationServiceContext(ServiceFactory.CreateOrganizationService(userId));
        }

        public WorkflowActivityContext(CodeActivityContext codeActivityContext)
        {
            CodeActivityContext = codeActivityContext;
            TracingService = codeActivityContext.GetExtension<ITracingService>();
            WorkflowContext = codeActivityContext.GetExtension<IWorkflowContext>();
            ServiceFactory = codeActivityContext.GetExtension<IOrganizationServiceFactory>();
        }

        public Guid InitiatingUserId { get { return WorkflowContext.InitiatingUserId; } }

        public void Trace(string format, params object[] args)
        {
            TracingService.Trace(format, args);
        }
    }
}
