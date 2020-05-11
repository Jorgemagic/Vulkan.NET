using System.Numerics;

namespace NVRTXHelloTriangle
{
    public struct Matrix3x4
    {
        //
        // Summary:
        //     Value at row 1 column 1.
        public float M11;
        //
        // Summary:
        //     Value at row 1 column 2.
        public float M12;
        //
        // Summary:
        //     Value at row 1 column 3.
        public float M13;
        //
        // Summary:
        //     Value at row 1 column 4.
        public float M14;
        //
        // Summary:
        //     Value at row 2 column 1.
        public float M21;
        //
        // Summary:
        //     Value at row 2 column 2.
        public float M22;
        //
        // Summary:
        //     Value at row 2 column 3.
        public float M23;
        //
        // Summary:
        //     Value at row 2 column 4.
        public float M24;
        //
        // Summary:
        //     Value at row 3 column 1.
        public float M31;
        //
        // Summary:
        //     Value at row 3 column 2.
        public float M32;
        //
        // Summary:
        //     Value at row 3 column 3.
        public float M33;
        //
        // Summary:
        //     Value at row 3 column 4.
        public float M34;

        public static Matrix3x4 ToMatrix3x4(Matrix4x4 m)
        {
            return new Matrix3x4()
            {
                M11 = m.M11,
                M12 = m.M12,
                M13 = m.M13,
                M14 = m.M14,
                M21 = m.M21,
                M22 = m.M22,
                M23 = m.M23,
                M24 = m.M24,
                M31 = m.M31,
                M32 = m.M32,
                M33 = m.M33,
                M34 = m.M34,
            };
        }
    }
}
