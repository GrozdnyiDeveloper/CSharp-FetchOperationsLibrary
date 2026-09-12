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
    public class ShareRecord : ShareStepBase
    {
        [Input("Read Permission")]
        [Default("True")]
        public InArgument<bool> ReadPermission { get; set; }

        [Input("Write Permission")]
        [Default("False")]
        public InArgument<bool> WritePermission { get; set; }

        [Input("Delete Permission")]
        [Default("False")]
        public InArgument<bool> DeletePermission { get; set; }

        [Input("Append Permission")]
        [Default("False")]
        public InArgument<bool> AppendPermission { get; set; }

        [Input("Append to Permission")]
        [Default("False")]
        public InArgument<bool> AppendToPermission { get; set; }

        [Input("Assign Permission")]
        [Default("False")]
        public InArgument<bool> AssignPermission { get; set; }

        [Input("Share Permission")]
        [Default("False")]
        public InArgument<bool> SharePermission { get; set; }

        public ShareRecord() 
        {
            _workflowName = "ShareRecord";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, EntityReference target, EntityReference systemUser)
        {
            // Формируем маску доступа на основе входных параметров
            var accessMask = BuildAccessMask(context);

            // Формируем запрос на предоставление доступа к записи
            GrantAccessRequest grantAccessRequest = new GrantAccessRequest()
            {
                Target = target,
                PrincipalAccess = new PrincipalAccess
                {
                    Principal = systemUser,
                    AccessMask = accessMask
                }
            };
            service.Execute(grantAccessRequest);
        }

        private AccessRights BuildAccessMask(WorkflowActivityContext context)
        {
            var accessMask = AccessRights.None;

            if (ReadPermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.ReadAccess;

            if (WritePermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.WriteAccess;

            if (DeletePermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.DeleteAccess;

            if (AppendPermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.AppendAccess;

            if (AppendToPermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.AppendToAccess;

            if (AssignPermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.AssignAccess;

            if (SharePermission.Get(context.CodeActivityContext))
                accessMask |= AccessRights.ShareAccess;

            return accessMask;
        }

        /// <summary>
        /// Метод указания входных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Получаем наследуемую строку 
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Read Permission: {ReadPermission.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Write Permission: {WritePermission.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Delete Permission: {DeletePermission.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Append Permission: {AppendPermission.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Append to Permission: {AppendToPermission.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Assign Permission: {AssignPermission.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Share Permission: {SharePermission.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(inputArguments.ToString());
        }
    }
}
