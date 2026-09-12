using CRMPark.FetchOperations.Base;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMPark.FetchOperations.Base
{
    public abstract class SearchStepBase : MainStepBase
    {
        [RequiredArgument]
        [Input("Key")]
        public InArgument<string> Key { get; set; }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем значение ключа
            var key = Key.Get(context.CodeActivityContext);

            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidPluginExecutionException($"Не были заполнены обязательные параметры шага.  ");
            }

            ExecuteActivity(context, service, key);
        }

        protected abstract void ExecuteActivity(
            WorkflowActivityContext context,
            IOrganizationService service,
            string key
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

            // Добавляем параметры шагов группы Search
            inputArguments.AppendLine($"Key: {Key.Get(context.CodeActivityContext)}");
            _resultText.Append(inputArguments.ToString());
        }
    }
}
