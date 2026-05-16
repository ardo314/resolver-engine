import z from "zod";

export const entityIdSchema = z.string().brand("EntityId");

export type EntityId = z.infer<typeof entityIdSchema>;
