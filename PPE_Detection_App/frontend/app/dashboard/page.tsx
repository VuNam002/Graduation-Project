"use client";

import { useAuth } from "../../lib/auth";
import { Button } from "@/components/ui/button";
import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function DashboardPage() {
  const { user, logout, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) {
      router.push("/login");
    }
  }, [user, loading, router]);

  if (loading) return <div className="flex items-center justify-center min-h-screen">Đang tải...</div>;
  
  if (!user) return null;

  return (
    <div className="flex flex-col items-center justify-center min-h-screen">
      <h1 className="text-2xl font-bold mb-4">Welcome to the Dashboard</h1>
      {user && (
        <div className="mb-4">
          <p>You are logged in.</p>
          {/* Display user information if available */}
        </div>
      )}
      <Button onClick={logout}>Logout</Button>
    </div>
  );
}
