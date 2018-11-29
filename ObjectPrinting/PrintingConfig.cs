using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions.Common;

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

        public IMemberPrintingConfig<TOwner, TPropType> Printing<TPropType>()
        {
            return new TypePrintingConfig<TOwner, TPropType>(this);
        }

        public IMemberPrintingConfig<TOwner, TPropType> Printing<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            if (!(memberSelector.Body is MemberExpression memberExpression))
                throw new ArgumentException("�������������� ��������� �� �������� ����������");
            
            var propName = memberExpression.Member;

            return new PropertyPrintingConfig<TOwner, TPropType>(this, (PropertyInfo)propName);
        }

        public PrintingConfig<TOwner> Excluding<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            if(!(memberSelector.Body is MemberExpression memberExpression))
                throw new ArgumentException("�������������� ��������� �� �������� ����������");

            propertiesToBeExcluded.Add(memberExpression.Member.Name);

            return this;
        }

        public PrintingConfig<TOwner> Excluding<TPropType>()
        {
            typesToBeExcluded.Add(typeof(TPropType));

            return this;
        }

        public string PrintToString(TOwner obj, char indentSymbol = '\t', int maxNestingLevel = 10,
            MemberTypes requiredMemberTypes = MemberTypes.Field | MemberTypes.Property)
        {
            return PrintToString(obj, 0, indentSymbol, maxNestingLevel, requiredMemberTypes);
        }

        private string PrintToString(object obj, int nestingLevel, char indentSymbol,
            int maxNestingLevel, MemberTypes requiredMemberTypes)
        {
            if (nestingLevel >= maxNestingLevel)
                return "WTF";
//                throw new OverflowException("�������� ������������ ������� �����������");

            if (obj == null)
                return "null" + Environment.NewLine;

            var type = obj.GetType();

            if (IsSimpleType(type))
                return obj + Environment.NewLine;

            var indentation = new string(indentSymbol, nestingLevel + 1);
            var sb = new StringBuilder();

            sb.AppendLine(type.Name);

            if (type.Implements(typeof(IEnumerable)))
            {
                var childPosition = 0;
                foreach (var value in (IEnumerable) obj)
                {
                    sb.Append(indentation + $"[{childPosition}] = " +
                              PrintToString(value, nestingLevel + 1, indentSymbol, maxNestingLevel,
                                  requiredMemberTypes));
                    childPosition++;
                }
            }
            else
            {
                foreach (var memberInfo in GetRequiredMembers(obj, requiredMemberTypes))
                {
                    if (!typesToBeExcluded.Contains(memberInfo.MemberType)
                        && !propertiesToBeExcluded.Contains(memberInfo.MemberName))
                    {
                        var value = memberInfo.MemberValue;

                        if (typesToBeAlternativelySerialized.ContainsKey(memberInfo.MemberType)
                            && !propertiesToBeAlternativelySerialized.ContainsKey(memberInfo.MemberName))
                            value = typesToBeAlternativelySerialized[memberInfo.MemberType].DynamicInvoke(value);

                        if (numericTypesToBeAlternativelySerializedUsingCultureInfo.ContainsKey(memberInfo.MemberType))
                            value = Convert.ToString(value,
                                numericTypesToBeAlternativelySerializedUsingCultureInfo[memberInfo.MemberType]);

                        if (propertiesToBeAlternativelySerialized.ContainsKey(memberInfo.MemberName))
                            value = propertiesToBeAlternativelySerialized[memberInfo.MemberName].DynamicInvoke(value);

                        sb.Append(indentation + memberInfo.MemberName + " = " +
                                  PrintToString(value, nestingLevel + 1, indentSymbol, maxNestingLevel,
                                      requiredMemberTypes));
                    }
                }
            }

            return sb.ToString();
        }

        private static IEnumerable<MemberMeta> GetRequiredMembers(object obj, MemberTypes requiredMemberTypes)
        {
            var type = obj.GetType();

            switch ((int)requiredMemberTypes)
            {
                case 4:
                    return type.GetFields().Select(e
                        => new MemberMeta { MemberType = e.FieldType, MemberValue = e.GetValue(obj), MemberName = e.Name});
                case 16:
                    return type.GetProperties().Select(e
                        => new MemberMeta { MemberType = e.PropertyType, MemberValue = e.GetValue(obj), MemberName = e.Name });
                case 20:
                    return type.GetFields().Select(e
                        => new MemberMeta { MemberType = e.FieldType, MemberValue = e.GetValue(obj), MemberName = e.Name }).Concat(
                        type.GetProperties().Select(e
                            => new MemberMeta { MemberType = e.PropertyType, MemberValue = e.GetValue(obj), MemberName = e.Name }));
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Determine whether a type is simple (String, Decimal, DateTime, etc) 
        /// or complex (i.e. custom class with public properties and methods).
        /// </summary>
        private static bool IsSimpleType(Type type)
        {
            return
                type.IsValueType ||
                type.IsPrimitive ||
                new[] {
                    typeof(String),
                    typeof(Decimal),
                    typeof(DateTime),
                    typeof(DateTimeOffset),
                    typeof(TimeSpan),
                    typeof(Guid)
                }.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object;
        }
    }
}