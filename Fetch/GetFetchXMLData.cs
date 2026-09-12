using CRMPark.FetchOperations.Base;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace CRMPark.FetchOperations.Fetch
{
    public class GetFetchXMLData : FetchStepBase
    {
        [Output("GUID")]
        public OutArgument<string> GUID { get; set; }

        [Output("Target Value 1")]
        public OutArgument<string> TargetValue1 { get; set; }

        [Output("Target Value 2")]
        public OutArgument<string> TargetValue2 { get; set; }

        [Output("Target Value 3")]
        public OutArgument<string> TargetValue3 { get; set; }

        [Output("Target Value 4")]
        public OutArgument<string> TargetValue4 { get; set; }

        public GetFetchXMLData()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "GetFetchXMLData";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Берём первую запись из списка и заполняем выходные параметры её данными (GUID и первые 4 атрибута)
                var entity = entites[0];
                GUID.Set(context.CodeActivityContext, entity.Id.ToString());
                if (CheckOutputValue(attributeNames, entity, 0))
                {
                    TargetValue1.Set(context.CodeActivityContext, GetStringValue(entity, attributeNames[0]));
                }
                if (CheckOutputValue(attributeNames, entity, 1))
                {
                    TargetValue2.Set(context.CodeActivityContext, GetStringValue(entity, attributeNames[1]));
                }
                if (CheckOutputValue(attributeNames, entity, 2))
                {
                    TargetValue3.Set(context.CodeActivityContext, GetStringValue(entity, attributeNames[2]));
                }
                if (CheckOutputValue(attributeNames, entity, 3))
                {
                    TargetValue4.Set(context.CodeActivityContext, GetStringValue(entity, attributeNames[3]));
                }
                _processedAmount = entites.Count;
            }
            else
            {
                // Если не получили записи из CRM, то устанавливаем значения по умолчанию
                SetDefaultOutputValues(context.CodeActivityContext);
            }
        }

        /// <summary>
        /// Метод проверки наличия указанных атрибутов в полученной записи
        /// </summary>
        /// <param name="attributeNames">Список получаемых запросом атрибутов в крректном порядке</param>
        /// <param name="entity">Данные полученной записи</param>
        /// <param name="index">Номер требуемого атрибута</param>
        /// <returns></returns>
        protected bool CheckOutputValue(List<string> attributeNames, Entity entity, int index)
        {
            return attributeNames.Count > index && entity.Contains(attributeNames[index]) && entity[attributeNames[index]] != null;
        }

        /// <summary>
        /// Метод указание выходных значений по умолчанию
        /// </summary>
        /// <param name="context">Контекст кастомного шага</param>
        protected void SetDefaultOutputValues(CodeActivityContext context)
        {
            this.GUID.Set(context, string.Empty);
            this.TargetValue1.Set(context, string.Empty);
            this.TargetValue2.Set(context, string.Empty);
            this.TargetValue3.Set(context, string.Empty);
            this.TargetValue4.Set(context, string.Empty);
        }

        /// <summary>
        /// Получение строкового значения из CRM в зависимости от его типа
        /// </summary>
        /// <param name="value">Изначальное значение</param>
        /// <returns>Строковое значение</returns>
        protected string GetStringValue(Entity entity, string attributeName)
        {
            var value = entity.Attributes[attributeName];

            // Для ссылки на запись получаем только её GUID 
            if (value is EntityReference entityRef)
            {
                return entityRef.Id.ToString();
            }

            // Для параметра получаем его фактическое значение
            if (value is OptionSetValue option)
            {
                return option.Value.ToString();
            }

            // Для валюты получаем её фактическое значение
            if (value is Money money)
            {
                return money.Value.ToString();
            }

            // Для даты получает отформатированное значение
            if (value is DateTime dateTime)
            {
                return entity.FormattedValues[attributeName];
            }

            // При пустом вводе вызвращаем также пустоты
            if (value is null)
            {
                return null;
            }

            // В остальных слуаях просто конвертируем значение в строку
            return value.ToString();
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
            inputArguments.AppendLine(string.Empty);
            _resultText.AppendLine(inputArguments.ToString());
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
            outputArguments.AppendLine($"GUID: {GUID.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Target Value 1: {TargetValue1.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Target Value 2: {TargetValue2.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Target Value 3: {TargetValue3.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Target Value 4: {TargetValue4.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
