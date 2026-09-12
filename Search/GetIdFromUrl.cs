using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CRMPark.FetchOperations.Search
{
    public class GetIdFromUrl : SearchStepBase
    {
        [Output("Record Id")]
        public OutArgument<string> RecordId { get; set; }

        public GetIdFromUrl()
        {
            _workflowName = "GetIdFromUrl";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, string key)
        {
            // Получаем строку с ID
            var recordId = _helper.GetIdFromUrl(key);

            // Проверяем её на заполнение и корректность преобразования
            if (recordId == null || recordId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException($"Не удалось получить ID записи из указанной строки {key}");
            }

            // Формируем значения для выходных параметров
            RecordId.Set(context.CodeActivityContext, recordId.ToString());
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
            outputArguments.AppendLine($"Record Id: {RecordId.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
