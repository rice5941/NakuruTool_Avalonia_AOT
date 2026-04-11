fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("nakuru_stretch")
        .csharp_namespace("NakuruTool_Avalonia_AOT.Features.BeatmapGenerator")
        .csharp_class_name("NativeStretchMethods")
        .csharp_class_accessibility("internal")
        .csharp_use_function_pointer(true)
        .generate_csharp_file(
            "../../NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/BeatmapGenerator/NativeStretchMethods.g.cs",
        )
        .unwrap();
}
