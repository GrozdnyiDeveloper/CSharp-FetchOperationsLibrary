using CRMPark.FetchOperations.Base;
using Microsoft.SqlServer.Server;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CRMPark.FetchOperations.Search
{
    public class FindSettingByKey : SearchStepBase
    {
        [Output("Setting")]
        [ReferenceTarget("systemuser")]
        public OutArgument<EntityReference> Setting { get; set; }

        public FindSettingByKey()
        {
            _workflowName = "FindSettingByKey";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, string key)
        {
            // Устанавливаем выходной EntityReference на null
            Setting.Set(context.CodeActivityContext, null);

            // Производим попытку получения записи Параметра по указанному ключу
            var setting = _helper.GetSettingByKey(service, key);

            // Формируем значения для выходных параметров
            Setting.Set(context.CodeActivityContext, setting.ToEntityReference());
        }

        /// <summary>
        /// Метод указания выходных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetOutputArguments(WorkflowActivityContext context)
        {
            // Получаем наследуемую строку 
            base.GetOutputArguments(context);
            StringBuilder outputArguments = new StringBuilder();
            outputArguments.AppendLine($"Setting: {Setting.Get(context.CodeActivityContext)?.Id}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
