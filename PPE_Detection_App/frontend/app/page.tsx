"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../lib/auth";

export default function Home() {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading) {
      if (user) {
        router.push("/admin");
      } else {
        router.push("/login");
      }
    }
  }, [isLoading, user, router]);

  return <div>Loading...</div>;
}