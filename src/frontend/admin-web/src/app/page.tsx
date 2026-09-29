import { redirect } from "next/navigation";
import { getRenderSession } from "@/lib/auth/get-render-session";

export default async function Home() {
  const session = await getRenderSession();
  if (!session) {
    redirect("/login?callbackUrl=/products");
  }

  redirect("/products");
}
