using System;
using System.Collections.Generic;
using System.Text;

namespace WC.SARS
{
    /* I wish to thank the developers of dnSpy for without it much of this would be impossible. Most notable this section where
     * I have absolutely no clue where they got there Mersenne-Twister algorithm from, so I cannot steal it as well. Instead all
     * I can do is just look at the compiled version in the assembly and re-create that.
     * Alternatively, I could've just you know, reference it; but that's too much effort/too many things that wouldn't be needed later
     * 
     * Thank you, so much.
     */

    /* either a copy-paste or re-writing it so it can actually work. even though the this.[whatever] are not really needed/were added
     * by the compiler, they are kept just... well just cause OK?
     * some casts were removed though! :) [[BAD IDEA]]
     * ^ -> all casts which *appear* to have been made by the compiler were removed. makes it slightly more readable
     */
    internal class MersenneTwister
    {
        //Create :) [[There are 2 but no clue if the other one is actually used no no one ever bothered bringing that one over]]
        public MersenneTwister(uint seed)
        {
            this.Seed = seed;
        }


        #region mt_variables
        //
        // Constants -- why are these all unused??
        /*
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 2567483615U;
        private const uint UPPER_MASK = 2147483648U;
        private const uint LOWER_MASK = 2147483647U;
        private const uint TEMPERING_MASK_B = 2636928640U;
        private const uint TEMPERING_MASK_C = 4022730752U;
        */
        //
        //PAIN SECTION
        private uint[] mt = new uint[624];
        private short mti;


        private uint seed_; // mhmm
        private uint Seed //mm yeah yeah mm yeah yeah
        {
            get
            {
                return this.seed_;
            }
            set
            {
                this.seed_ = value;
                this.mt[0] = (this.seed_ & uint.MaxValue);
                this.mti = 1;
                while (this.mti < 624) //could learn something from this for loop...
                {
                    this.mt[this.mti] = (69069U * this.mt[this.mti - 1] & uint.MaxValue);
                    this.mti += 1;
                }
            }
        }
        private static uint[] mag01 = new uint[]
        {
        0U,
        2567483615U
        };
        //
        #endregion mt_variables

        #region tempering shifts
        // LUST lol
        private static uint TEMPER_SHIFT_L(uint y)
        {
            return y >> 18;
        }
        private static uint TEMPER_SHIFT_U(uint y)
        {
            return y >> 11;
        }
        private static uint TEMPER_SHIFT_S(uint y)
        {
            return y << 7;
        }
        private static uint TEMPER_SHIFT_T(uint y)
        {
            return y << 15;
        }
        #endregion tempering shifts

        // Generate U-Int -- The base of all this fun stuff! :D
        protected uint GenerateUInt()
        {
            uint retValue;

            //calculation tiem
            if (this.mti >= 624)
            {
                short num;
                for (num = 0; num < 227; num++) //++ IS the same as [num] += 1
                {
                    retValue = ( (this.mt[num] & 2147483648U) | (this.mt[num+1] & 2147483647U) );
                    this.mt[num] = ( this.mt[num+397] ^ retValue >> 1 ^ MersenneTwister.mag01[(int)(UIntPtr)(retValue & 1U)] );
                    //so the (int)(uIntPtr)(thing) is how is looks in decomped code. THIS version gets turned into...
                    // (int)<<(UINT)>>(uintPTR)(thing) -- this *should* be fine, but there may be some unexpected results because of this
                }
                while (num < 623)
                {
                    retValue = ( (this.mt[num] & 2147483648U) | (this.mt[num+1] & 2147483647U) );
                    this.mt[num] = ( this.mt[num + -227] ^ retValue >> 1 ^ MersenneTwister.mag01[(int)(UIntPtr)(retValue & 1U)]);
                    num += 1;
                }
                retValue = ( (this.mt[623] & 2147483648U) | (this.mt[0] & 2147483647U));
                this.mt[623] = ( this.mt[396] ^ retValue >> 1 ^ MersenneTwister.mag01[(int)(UIntPtr)(retValue & 1U)] );
                this.mti = 0;
            }

            //end
            uint[] array = this.mt;
            short confuze;
            //confuze = this.mti;
            //this.mti = (short)(confuze + 1);
            this.mti = (short)((confuze = this.mti) + 1);
            retValue = array[confuze];

            /*
	        num2 ^= MersenneTwister.TEMPERING_SHIFT_U(num2);
	        num2 ^= (MersenneTwister.TEMPERING_SHIFT_S(num2) & 2636928640U);
	        num2 ^= (MersenneTwister.TEMPERING_SHIFT_T(num2) & 4022730752U);
	        return num2 ^ MersenneTwister.TEMPERING_SHIFT_L(num2);
            */
            retValue ^= MersenneTwister.TEMPER_SHIFT_U(retValue);
            retValue ^= ( MersenneTwister.TEMPER_SHIFT_S(retValue) & 2636928640U );
            retValue ^= ( MersenneTwister.TEMPER_SHIFT_T(retValue) & 4022730752U);
            return retValue ^ MersenneTwister.TEMPER_SHIFT_L(retValue);
        }
        //Could implement other next-whatever methods. only one of interest for item/loot gen is this.
        public virtual uint NextUInt(uint minValue, uint maxValue)
        {
            if (minValue < maxValue)
            {
                //uint returnThis = (uint)(this.GenerateUInt() / (4294967295.0 / (maxValue - minValue)) + minValue);
                //Logger.DebugServer($"This return: {returnThis}");
                //return returnThis;
                return (uint)(this.GenerateUInt() / (4294967295.0 / (maxValue - minValue)) + minValue);
            }
            if (minValue == maxValue)
            {
                //Logger.DebugServer($"Min-Max Return: This return: {minValue}");
                return minValue;
            }
            throw new ArgumentOutOfRangeException("minValue", "NextUInt() called with minValue >= maxValue");
        }
    }
}
