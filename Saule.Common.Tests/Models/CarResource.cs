﻿namespace Saule.Common.Tests.Models
{
    public class CarResource : ApiResource
    {
        public CarResource()
        {
            WithId(nameof(Car.Id));
            Attribute(nameof(Car.Model));
        }
    }
}