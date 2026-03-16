'use client';

import { AppSidebar } from "@/components/app-sidebar";
import { ChartAreaInteractive } from "@/components/chart-area-interactive";
import { ChartPieSimple } from "@/components/chart-pie-simple";
import { ChartBarDefault } from "@/components/chart-bar-default";
import { SectionCards } from "@/components/section-cards";
import { SiteHeader } from "@/components/site-header";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { ChartLineDots } from "@/components/chart-line-dots";
import { WidgetsBarChart } from "@/components/barChart";
import { NewViolationsRadial } from "@/components/radialChart";

import { useEffect } from "react";
import useSignalR from "@/hooks/use-signalr";
import { Toaster, toast } from "sonner";
import Image from 'next/image';

interface ViolationNotificationDto {
  message: string;
  imageUrl: string;
  timestamp: string;
  violationType: string;
}

const NotificationToast = ({ message, imageUrl }: { message: string, imageUrl: string }) => (
    <div className="flex items-center space-x-4">
        <div className="flex-shrink-0">
            <Image 
                src={imageUrl} 
                alt="Violation Image" 
                width={80} 
                height={80}
                className="rounded-md object-cover"
            />
        </div>
        <div>
            <div className="font-semibold">Phát hiện vi phạm!</div>
            <div className="text-sm text-gray-600">{message}</div>
        </div>
    </div>
);


export default function Page() {
  const connection = useSignalR('/notificationHub');

  useEffect(() => {
    if (connection) {
      connection.on("ReceiveViolation", (notification: ViolationNotificationDto) => {
        console.log("Received new violation:", notification);
        toast(<NotificationToast message={notification.message} imageUrl={notification.imageUrl} />, {
          duration: 10000, 
          position: "top-right",
        });
      });
      return () => {
        connection.off("ReceiveViolation");
      };
    }
  }, [connection]);

  return (
    <SidebarProvider
      style={
        {
          "--sidebar-width": "calc(var(--spacing) * 72)",
          "--header-height": "calc(var(--spacing) * 12)",
        } as React.CSSProperties
      }
    >
      <Toaster richColors />
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div className="flex flex-1 flex-col">
          <div className="@container/main flex flex-1 flex-col gap-2">
            <div className="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
              <SectionCards />
              <div className="px-4 lg:px-6">
                <ChartAreaInteractive />
              </div>
              <div className="px-4 lg:px-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                <ChartPieSimple />
                <ChartBarDefault />
                <ChartLineDots />
              </div>
              <div className="px-4 lg:px-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                <NewViolationsRadial />
                <WidgetsBarChart />
              </div>
            </div>
          </div>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
