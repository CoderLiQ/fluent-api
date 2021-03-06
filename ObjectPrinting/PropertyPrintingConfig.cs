﻿using System;
using System.Globalization;
using System.Reflection;

namespace ObjectPrinting
{
    public class PropertyPrintingConfig<TOwner, TPropType> : IMemberPrintingConfig<TOwner, TPropType>
    {
        private readonly PrintingConfig<TOwner> printingConfig;
        private readonly PropertyInfo propertyInfo;

        public PropertyPrintingConfig(PrintingConfig<TOwner> printingConfig, PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
            this.printingConfig = printingConfig;
        }

        public PrintingConfig<TOwner> Using(Func<TPropType, string> print)
        {
            printingConfig.AddPropertyToBeAlternativelySerialized(propertyInfo.Name, print);

            return printingConfig;
        }

        public PrintingConfig<TOwner> Using(CultureInfo culture)
        {
            if(!typeof(TPropType).IsNumericType())
                throw new ArgumentException("Использованный тип не является допустимым.");

            printingConfig
                .AddNumericTypeToBeAlternativelySerializedUsingCultureInfo(typeof(TPropType), culture);
            return printingConfig;
        }
    }
}