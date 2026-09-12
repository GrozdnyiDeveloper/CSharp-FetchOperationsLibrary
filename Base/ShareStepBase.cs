using CRMPark.FetchOperations.Base;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace CRMPark.FetchOperations.Base
{
    public abstract class ShareStepBase : MainStepBase
    {
        [RequiredArgument]
        [Input("Sharing Record")]
        public InArgument<string> SharingRecord { get; set; }

        [RequiredArgument]
        [Input("Entity Name")]
        public InArgument<string> EntityName { get; set; }

        [RequiredArgument]
        [Input("User")]
        [ReferenceTarget("systemuser")]
        public InArgument<EntityReference> User { get; set; }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем входные параметры
            var sharingRecord = SharingRecord.Get(context.CodeActivityContext);
            var entityName = EntityName.Get(context.CodeActivityContext);
            var systemUser = User.Get(context.CodeActivityContext);

            // Проверяем заполнение обязательных параметров
            if (string.IsNullOrEmpty(sharingRecord) || string.IsNullOrEmpty(entityName) || systemUser == null)
            {
                throw new InvalidPluginExecutionException($"Не были заполнены обязательные параметры шага. ");
            }

            // Формируем ссылку на запись к которой будет выдаваться/отбираться доступ
            var target = new EntityReference(entityName, _helper.GetIdFromUrl(sharingRecord));

            ExecuteActivity(context, service, target, systemUser);
        }

        protected abstract void ExecuteActivity(
            WorkflowActivityContext context,
            IOrganizationService service,
            EntityReference target, 
            EntityReference systemUser
        );

        /// <summary>
        /// Метод указания входных параметров шага, наследуемый из Main
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Получаем строку от Main
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();

            // Добавляем параметры шагов группы Share
            inputArguments.AppendLine($"Sharing Record: {SharingRecord.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Entity Name: {EntityName.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"User: {User.Get(context.CodeActivityContext).Id}");
            _resultText.Append(inputArguments.ToString());
        }
    }
}
