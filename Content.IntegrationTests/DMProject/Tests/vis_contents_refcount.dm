var/global/vis_contents_ref_deleted = FALSE

/obj/vis_contents_ref
/obj/vis_contents_ref/Del()
	vis_contents_ref_deleted = TRUE
	..()

/proc/add_new_to_vis_contents(atom/holder)
	holder.vis_contents += new /obj/vis_contents_ref

/datum/unit_test/vis_contents_refcount/RunTest()
	var/obj/holder = new(locate(2, 2, 1))

	// vis_contents keeps what's in it alive
	add_new_to_vis_contents(holder)
	ASSERT(!vis_contents_ref_deleted)

	// Assigning it to itself doesn't empty it
	holder.vis_contents = holder.vis_contents
	ASSERT(length(holder.vis_contents) == 1)
	ASSERT(!vis_contents_ref_deleted)

	// Removing the last reference collects it
	holder.vis_contents.Cut()
	ASSERT(vis_contents_ref_deleted)

	vis_contents_ref_deleted = FALSE
	add_new_to_vis_contents(holder)
	holder.vis_contents -= holder.vis_contents[1]
	ASSERT(vis_contents_ref_deleted)

	// Deleting the holder releases its vis_contents
	vis_contents_ref_deleted = FALSE
	add_new_to_vis_contents(holder)
	del(holder)
	ASSERT(vis_contents_ref_deleted)
