using System;
using System.Collections.Generic;
using System.Text;

namespace WC.SARS
{
    public enum VehicleType
    {
        Hamsterball,
        Emu
    }
    internal class Vehicle
    {
        public byte HP;
        public short VehicleID;
        public Vehicle(byte hp, short vehicleid)
        {
            HP = hp;
            VehicleID = vehicleid;
        }
    }
}
