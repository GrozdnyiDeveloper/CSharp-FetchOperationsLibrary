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
    public abstract class RegexStepBase : MainStepBase
    {
        [RequiredArgument]
        [Input("Source String")]
        public InArgument<string> SourceString { get; set; }

        [RequiredArgument]
        [Input("Regular Expression")]
        public InArgument<string> RegularExpression { get; set; }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем входные параметры
            var sourceString = SourceString.Get(context.CodeActivityContext);
            var regularExpression = RegularExpression.Get(context.CodeActivityContext);

            // Проверяем заполнение обязательных параметров
            if (string.IsNullOrEmpty(sourceString) || string.IsNullOrEmpty(regularExpression))
            {
                throw new InvalidPluginExecutionException($"Не были заполнены обязательные параметры шага. ");
            }

            ExecuteActivity(context, service, sourceString, regularExpression);
        }

        protected abstract void ExecuteActivity(
            WorkflowActivityContext context,
            IOrganizationService service,
            string sourceString,
            string regularExpression
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

            // Добавляем параметры шагов группы Regex
            inputArguments.AppendLine($"Source String: {SourceString.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Regular Expression: {RegularExpression.Get(context.CodeActivityContext)}");
            _resultText.Append(inputArguments.ToString());
        }
    }
}
