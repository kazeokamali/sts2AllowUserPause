extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		printerr("Usage: pack_pck.gd <out.pck> <mod_manifest.json>")
		quit(2)
		return

	var out_pck := args[0]
	var manifest_src := args[1]
	var pck_name := _resolve_pck_name(manifest_src, out_pck)

	if not FileAccess.file_exists(manifest_src):
		printerr("Missing manifest file: ", manifest_src)
		quit(3)
		return

	var packer := PCKPacker.new()
	var err := packer.pck_start(out_pck)
	if err != OK:
		printerr("pck_start failed with code: ", err)
		quit(10)
		return

	err = _add_file(packer, "res://mod_manifest.json", manifest_src)
	if err != OK:
		quit(11)
		return

	err = _pack_localization_tables(packer, pck_name)
	if err != OK:
		quit(12)
		return

	err = _pack_project_resources(packer)
	if err != OK:
		quit(13)
		return

	err = _pack_mod_images(packer, pck_name)
	if err != OK:
		quit(14)
		return

	err = packer.flush()
	if err != OK:
		printerr("flush() failed with code: ", err)
		quit(15)
		return

	print("Packed: ", out_pck)
	quit(0)

func _resolve_pck_name(manifest_src: String, out_pck: String) -> String:
	var pck_name := out_pck.get_file().get_basename()
	var raw_json := FileAccess.get_file_as_string(manifest_src)
	if raw_json.is_empty():
		return pck_name

	var parsed: Variant = JSON.parse_string(raw_json)
	if parsed is Dictionary and parsed.has("pck_name"):
		var from_manifest := str(parsed["pck_name"]).strip_edges()
		if not from_manifest.is_empty():
			pck_name = from_manifest

	return pck_name

func _pack_localization_tables(packer: PCKPacker, pck_name: String) -> int:
	var root := ProjectSettings.globalize_path("res://")
	var localization_root := root.path_join("localization")
	if not DirAccess.dir_exists_absolute(localization_root):
		return OK

	var languages := DirAccess.get_directories_at(localization_root)
	for language in languages:
		var language_dir := localization_root.path_join(language)
		for file_name in DirAccess.get_files_at(language_dir):
			if not file_name.ends_with(".json"):
				continue

			var source_path := language_dir.path_join(file_name)
			var dest_path := "res://%s/localization/%s/%s" % [pck_name, language, file_name]
			var err := _add_file(packer, dest_path, source_path)
			if err != OK:
				return err

	return OK

func _pack_mod_images(packer: PCKPacker, pck_name: String) -> int:
	var root := ProjectSettings.globalize_path("res://")
	var images_root := root.path_join("images")
	if not DirAccess.dir_exists_absolute(images_root):
		return OK

	return _pack_directory_recursive(packer, images_root, "res://%s/images" % pck_name)

func _pack_project_resources(packer: PCKPacker) -> int:
	var root := ProjectSettings.globalize_path("res://")
	var user_tscn_root := root.path_join("userTscn")
	if not DirAccess.dir_exists_absolute(user_tscn_root):
		return OK

	return _pack_directory_recursive(packer, user_tscn_root, "res://userTscn")

func _pack_directory_recursive(packer: PCKPacker, source_dir: String, dest_dir: String) -> int:
	for file_name in DirAccess.get_files_at(source_dir):
		if file_name.ends_with(".import"):
			continue

		var source_path := source_dir.path_join(file_name)
		var dest_path := dest_dir.path_join(file_name)
		var err := _add_file(packer, dest_path, source_path)
		if err != OK:
			return err

	for dir_name in DirAccess.get_directories_at(source_dir):
		var err := _pack_directory_recursive(
			packer,
			source_dir.path_join(dir_name),
			dest_dir.path_join(dir_name))
		if err != OK:
			return err

	return OK

func _add_file(packer: PCKPacker, dest_path: String, source_path: String) -> int:
	var err := packer.add_file(dest_path, source_path)
	if err != OK:
		printerr("Failed to add file: ", source_path, " -> ", dest_path, " code: ", err)
	return err
