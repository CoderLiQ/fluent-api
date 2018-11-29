using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ObjectPrinting
{
    public class PrintingConfig<TOwner>
    {
        private readonly List<Type> typesToBeExcluded = new List<Type>();
        private readonly List<string> propertiesToBeExcluded = new List<string>();

        private readonly Dictionary<Type, Delegate> typesToBeAlternativelySerialized = new Dictionary<Type, Delegate>();

        private readonly Dictionary<string, Delegate> propertiesToBeAlternativelySerialized = new Dictionary<string, Delegate>();

        private readonly Dictionary<Type, CultureInfo> numericTypesToBeAlternativelySerializedUsingCultureInfo 
            = new Dictionary<Type, CultureInfo>();

        public void AddTypeToBeAlternativelySerialized(Type type, Delegate del) 
            => typesToBeAlternativelySerialized[type] = del;

        public void AddPropertyToBeAlternativelySerialized(string propertyName, Delegate del) 
            => propertiesToBeAlternativelySerialized[propertyName] = del;

        public void AddNumericTypeToBeAlternativelySerializedUsingCultureInfo(Type type, CultureInfo cultureInfo)
            => numericTypesToBeAlternativelySerializedUsingCultureInfo[type] = cultureInfo;

        public PropertyPrintingConfig<TOwner, TPropType> Printing<TPropType>()
        {
            return new PropertyPrintingConfig<TOwner, TPropType>(this);
        }

        public PropertyPrintingConfig<TOwner, TPropType> Printing<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            MemberExpression selectorBody;
            try
            {
                selectorBody = (MemberExpression)memberSelector.Body;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException("�������������� ��������� �� �������� ����������");
            }
            var propName = selectorBody.Member;

            return new PropertyPrintingConfig<TOwner, TPropType>(this, (PropertyInfo)propName);
        }

        public PrintingConfig<TOwner> Excluding<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            MemberExpression memberExpression;
            try
            {
                memberExpression = (MemberExpression)memberSelector.Body;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException("�������������� ��������� �� �������� ����������");
            }

            propertiesToBeExcluded.Add(memberExpression.Member.Name);
            return this;
        }

        public PrintingConfig<TOwner> Excluding<TPropType>()
        {
            typesToBeExcluded.Add(typeof(TPropType));

            return this;
        }

        public string PrintToString(TOwner obj, char indentSymbol = '\t', int maxNestingLevel = 10)
        {
            return PrintToString(obj, 0, indentSymbol, maxNestingLevel);
        }

        private string PrintToString(object obj, int nestingLevel, char indentSymbol, int maxNestingLevel)
        {
            if (nestingLevel >= maxNestingLevel)
                throw new OverflowException("�������� ������������ ������� �����������");

            if (obj == null)
                return "null" + Environment.NewLine;

            var type = obj.GetType();

            if (type.IsSimpleType())
                return obj + Environment.NewLine;

            var indentation = new string(indentSymbol, nestingLevel + 1);
            var sb = new StringBuilder();

            sb.AppendLine(type.Name);

            var members = new List<MemberInfo>();
            
            members.AddRange(type.GetFields());
            members.AddRange(type.GetProperties());

            foreach (var memberInfo in members)
            {
                var propType = memberInfo.GetUnderlyingType();

                var propName = memberInfo.Name;

                if (!typesToBeExcluded.Contains(propType)
                    && !propertiesToBeExcluded.Contains(propName))
                {
                    var value = memberInfo.GetValue(obj);

                    if (typesToBeAlternativelySerialized.ContainsKey(propType)
                        && !propertiesToBeAlternativelySerialized.ContainsKey(propName))
                        value = typesToBeAlternativelySerialized[propType].DynamicInvoke(value);

                    if (numericTypesToBeAlternativelySerializedUsingCultureInfo.ContainsKey(propType))
                        value = Convert.ToString(value,
                            numericTypesToBeAlternativelySerializedUsingCultureInfo[propType]);

                    if (propertiesToBeAlternativelySerialized.ContainsKey(propName))
                        value = propertiesToBeAlternativelySerialized[propName].DynamicInvoke(value);

                    sb.Append(indentation + propName + " = " +
                              PrintToString(value, nestingLevel + 1, indentSymbol, maxNestingLevel));
                }
            }

            return sb.ToString();
        }
    }
}