fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("analyze_7k")
        .csharp_namespace("NakuruTool_Avalonia_AOT.Features.Analyze7k")
        .csharp_class_name("NativeMethods")
        .csharp_class_accessibility("internal")
        .csharp_use_function_pointer(true)
        .generate_csharp_file("../../NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/Analyze7k/NativeMethods.g.cs")
        .unwrap();
}
