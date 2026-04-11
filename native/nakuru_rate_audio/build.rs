use std::env;
use std::path::PathBuf;

fn main() {
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-changed=cpp/");
    println!("cargo:rerun-if-changed=src/lib.rs");
    println!("cargo:rerun-if-changed=vendor/bungee/");

    let out_dir = PathBuf::from(env::var("OUT_DIR").unwrap());
    let bungee_dir = require_path(
        "vendor/bungee",
        "Bungee submodule is missing. Run `git submodule update --init --recursive` from the repository root.",
    );
    require_path(
        "vendor/bungee/CMakeLists.txt",
        "vendor/bungee/CMakeLists.txt was not found. The Bungee submodule may be incomplete.",
    );

    // 1. Bungee upstream を cmake でスタティックビルド
    build_bungee(&bungee_dir);

    // 2. C bridge を cc でビルド
    build_bridge(&bungee_dir);

    // 3. bindgen で bridge header → Rust FFI 生成
    generate_bindings(&out_dir);

    // 4. csbindgen で C# P/Invoke 生成
    generate_csharp_bindings();
}

fn require_path(relative_path: &str, message: &str) -> PathBuf {
    let path = PathBuf::from(relative_path);
    if path.exists() {
        path
    } else {
        panic!("{} (missing path: {})", message, path.display());
    }
}

fn build_bungee(bungee_dir: &PathBuf) {
    let profile = match env::var("PROFILE").unwrap().as_str() {
        "release" => "Release",
        _ => "RelWithDebInfo",
    };

    let target = env::var("TARGET").unwrap();
    let mut config = cmake::Config::new(bungee_dir);
    config
        .profile(profile)
        .define("BUNGEE_BUILD_SHARED_LIBRARY", "OFF")
        .define("CMAKE_POSITION_INDEPENDENT_CODE", "ON")
        .build_target("bungee_library");

    if target.contains("msvc") {
        config
            .cflag("/D_USE_MATH_DEFINES")
            .cxxflag("/D_USE_MATH_DEFINES")
            .cxxflag("/EHsc");
    }

    let dst = config.build();

    // Bungee + PFFFT スタティックライブラリのリンク
    let build_dir = dst.join("build");
    println!("cargo:rustc-link-search=native={}", build_dir.display());
    println!(
        "cargo:rustc-link-search=native={}/{}",
        build_dir.display(),
        profile
    );
    println!("cargo:rustc-link-lib=static=bungee");

    // PFFFT は bungee_library の PRIVATE 依存。静的リンク時は明示的にリンク必要。
    println!(
        "cargo:rustc-link-search=native={}/submodules/pffft",
        build_dir.display()
    );
    println!(
        "cargo:rustc-link-search=native={}/submodules/pffft/{}",
        build_dir.display(),
        profile
    );
    println!("cargo:rustc-link-lib=static=pffft");

    // C++ 標準ライブラリ (MSVC は自動)
    if !target.contains("msvc") {
        println!("cargo:rustc-link-lib=stdc++");
    }
}

fn build_bridge(bungee_dir: &PathBuf) {
    cc::Build::new()
        .cpp(true)
        .file("cpp/bungee_bridge.cpp")
        .include("cpp")
        .include(bungee_dir)
        .include(bungee_dir.join("submodules"))
        .include(bungee_dir.join("submodules/eigen"))
        .flag_if_supported("/EHsc")
        .flag_if_supported("/std:c++20")
        .flag_if_supported("-std=c++20")
        .compile("nakuru_bungee_bridge");
}

fn generate_bindings(out_dir: &PathBuf) {
    bindgen::Builder::default()
        .header("cpp/bungee_bridge.h")
        .allowlist_function("nakuru_bungee_.*")
        .allowlist_type("nakuru_bungee_.*")
        .generate()
        .expect("bindgen failed")
        .write_to_file(out_dir.join("bungee_bindings.rs"))
        .expect("write bindings failed");
}

fn generate_csharp_bindings() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("nakuru_rate_audio")
        .csharp_namespace("NakuruTool_Avalonia_AOT.Features.BeatmapGenerator")
        .csharp_class_name("NativeRateAudioMethods")
        .csharp_class_accessibility("internal")
        .csharp_use_function_pointer(true)
        .generate_csharp_file(
            "../../NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/BeatmapGenerator/NativeRateAudioMethods.g.cs",
        )
        .unwrap();
}
