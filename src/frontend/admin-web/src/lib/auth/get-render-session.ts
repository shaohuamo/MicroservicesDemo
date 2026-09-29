import { cache } from "react";
import { auth } from "@/auth";

export const getRenderSession = cache(() => auth());
