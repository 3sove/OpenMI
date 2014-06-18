using System;
using System.Globalization;
using Oatc.OpenMI.Sdk.Backbone;
using OpenMI.Standard2;
using OpenMI.Standard2.TimeSpace;

namespace Oatc.OpenMI.Sdk.Spatial
{
    /// <summary>
    /// An <see cref="ITimeSpaceAdaptedOutput"/> that multiplies the values of the
    /// valueset with the length of the element line to some power. By default the power is one.
    /// <para>
    /// Assumes that 
    /// the <see cref="IBaseAdaptedOutput.Adaptee"/> is an <see cref="ITimeSpaceAdaptedOutput"/>
    /// the <see cref="IElementSet"/> has <see cref="ElementType.PolyLine"/> as elements 
    /// in the <see cref="ITimeSpaceExchangeItem.SpatialDefinition"/>
    /// and <see cref="IValueDefinition.ValueType"/> as typeof(double).
    /// </para>
    /// </summary>
    public class ElementLineLengthOperationAdaptor : AbstractTimeSpaceAdaptor
    {
        private readonly ITimeSpaceOutput _tsadaptee;

        private double _lengthExponent = 1.0;
        private readonly ArgumentDouble _lengthExponentArgument = new ArgumentDouble("LengthExponent", 1.0);

        private double[] _factors;
        private Quantity _quantity;

        public ElementLineLengthOperationAdaptor(string id, ITimeSpaceOutput adaptee)
            : base(adaptee, id)
        {
            _tsadaptee = adaptee;
            Arguments.Add(_lengthExponentArgument);
            // Check the adaptee data
            IElementSet elementSet = adaptee.SpatialDefinition as IElementSet;
            if (elementSet == null)
                throw new ArgumentException("Adaptee must have an IElementSet as SpatialDefinition", "adaptee");
            if (elementSet.ElementType != ElementType.PolyLine)
                throw new ArgumentException("Adaptee must have a SpatialDefinition having polyline as elements", "adaptee");
            if (adaptee.ValueDefinition.ValueType != typeof(double))
                throw new ArgumentException("Adaptee valuetype must be typeof(double)", "adaptee");
            if (!(adaptee.ValueDefinition is IQuantity))
                throw new ArgumentException("Adaptee valueDefinition must be an IQuantity", "adaptee");
        }

        public override IValueDefinition ValueDefinition
        {
            get { return (_quantity); }
        }

        public override void Initialize()
        {
            _lengthExponent = _lengthExponentArgument.Value;
            IElementSet elementSet = _tsadaptee.ElementSet();
            _factors = new double[elementSet.ElementCount];

            // Update quantity to take area operation into account
            UpdateQuantity();

            // Setup area exponent
            CalculateFactors(elementSet);
        }

        protected void CalculateFactors(IElementSet elementSet)
        {
            for (int i = 0; i < elementSet.ElementCount; i++)
            {
                XYPolyline element = ElementMapper.CreateXYPolyline(elementSet, i);
                double length = element.GetLength();
                if (_lengthExponent == 1)
                    _factors[i] = length;
                else if (_lengthExponent == -1)
                    _factors[i] = 1.0 / length;
                else
                    _factors[i] = Math.Pow(length, _lengthExponent);
            }
        }

        private void UpdateQuantity()
        {
            IQuantity sourceQuantity = (IQuantity)_tsadaptee.ValueDefinition;

            Dimension dimension = new Dimension();
            foreach (DimensionBase dimBase in Enum.GetValues(typeof(DimensionBase)))
            {
                dimension.SetPower(dimBase, sourceQuantity.Unit.Dimension.GetPower(dimBase));
            }
            dimension.SetPower(DimensionBase.Length, dimension.GetPower(DimensionBase.Length) + _lengthExponent);

            string pu = " * m^" + (_lengthExponent).ToString(CultureInfo.InvariantCulture);
            string pq = " * length^" + _lengthExponent.ToString(CultureInfo.InvariantCulture);

            Unit unit = new Unit(sourceQuantity.Unit);
            unit.Caption = unit.Caption + pu;
            unit.Description = unit.Description + pu;
            unit.Dimension = dimension;

            _quantity = new Quantity(unit, sourceQuantity.Description + pq, sourceQuantity.Caption + pq);
        }

        public override ITimeSpaceValueSet GetValues(IBaseExchangeItem querySpecifier)
        {
            return (_tsadaptee.GetValues(querySpecifier).MultiplyElementValues(_factors));
        }

        public override ITimeSpaceValueSet Values
        {
            get { return (_tsadaptee.Values.MultiplyElementValues(_factors)); }
        }

    }
}