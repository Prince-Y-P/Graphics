﻿namespace UnityEngine.MaterialGraph
{
    [Title("Procedural/Voronoi Noise")]
    public class VoronoiNoiseNode : FunctionNInNOut, IGeneratesFunction
    {
        public VoronoiNoiseNode()
        {
            name = "VoronoiNoise";
            AddSlot("UV", "uv", Graphing.SlotType.Input, SlotValueType.Vector2, Vector4.zero);
            AddSlot("AngleOffset", "angleOffset", Graphing.SlotType.Input, SlotValueType.Vector1, new Vector4(2.0f,0,0,0));
            AddSlot("Cellular", "n1", Graphing.SlotType.Output, SlotValueType.Vector1, Vector4.zero);
            AddSlot("Tile", "n2", Graphing.SlotType.Output, SlotValueType.Vector1, Vector4.zero);
            AddSlot("1 - Cellular", "n3", Graphing.SlotType.Output, SlotValueType.Vector1, Vector4.zero);
        }

        protected override string GetFunctionName()
        {
            return "unity_voronoinoise_" + precision;
        }

        public override bool hasPreview
        {
            get
            {
                return true;
            }
        }

        public void GenerateNodeFunction(ShaderGenerator visitor, GenerationMode generationMode)
        {
            var outputString = new ShaderGenerator();

            outputString.AddShaderChunk("inline float2 unity_voronoi_noise_randomVector (float2 uv, float offset)", false);
            outputString.AddShaderChunk("{", false);
            outputString.Indent();

            outputString.AddShaderChunk("float2x2 m = float2x2(15.27, 47.63, 99.41, 89.98);", false);
            outputString.AddShaderChunk("uv = frac(sin(mul(uv, m)) * 46839.32);", false);
            outputString.AddShaderChunk("return float2(sin(uv.y*+offset)*0.5+0.5, cos(uv.x*offset)*0.5+0.5);", false);

            outputString.Deindent();
            outputString.AddShaderChunk("}", false);

            outputString.AddShaderChunk(GetFunctionPrototype(), false);
            outputString.AddShaderChunk("{", false);
            outputString.Indent();

            outputString.AddShaderChunk("float2 g = floor(uv);", false);
            outputString.AddShaderChunk("float2 f = frac(uv);", false);
            outputString.AddShaderChunk("float t = 8.0;", false);
            outputString.AddShaderChunk("float3 res = float3(8.0, 0.0, 0.0);", false);

            outputString.AddShaderChunk("for(int y=-1; y<=1; y++)", false);
            outputString.AddShaderChunk("{", false);
            outputString.Indent();

            outputString.AddShaderChunk("for(int x=-1; x<=1; x++)", false);
            outputString.AddShaderChunk("{", false);
            outputString.Indent();

            outputString.AddShaderChunk("float2 lattice = float2(x,y);", false);
            outputString.AddShaderChunk("float2 offset = unity_voronoi_noise_randomVector(lattice + g, angleOffset);", false);
            outputString.AddShaderChunk("float d = distance(lattice + offset, f);", false);
            outputString.AddShaderChunk("if(d < res.x)", false);
            outputString.AddShaderChunk("{", false);
            outputString.Indent();

            outputString.AddShaderChunk("res = float3(d, offset.x, offset.y);", false);
            outputString.AddShaderChunk("n1 = res.x;", false);
            outputString.AddShaderChunk("n2 = res.y;", false);
            outputString.AddShaderChunk("n3 = 1.0 - res.x;", false);

            outputString.Deindent();
            outputString.AddShaderChunk("}", false);

            outputString.Deindent();
            outputString.AddShaderChunk("}", false);

            outputString.Deindent();
            outputString.AddShaderChunk("}", false);


            outputString.Deindent();
            outputString.AddShaderChunk("}", false);

            visitor.AddShaderChunk(outputString.GetShaderString(0), true);
        }
    }
}
