fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("nakuru_audio")
        .csharp_namespace("NakuruTool_Avalonia_AOT.Features.AudioPlayer")
        .csharp_class_name("NativeMethods")
        .csharp_class_accessibility("internal")
        .csharp_use_function_pointer(true)
        .generate_csharp_file("../../NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/AudioPlayer/NativeMethods.g.cs")
        .unwrap();
}
