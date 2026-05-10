"use client"

import * as React from "react"
import {
  IconCamera,
  IconInnerShadowTop,
  IconListDetails,
  IconCashPlus ,
  IconXboxX ,
  IconAutomation,
} from "@tabler/icons-react"
import Link from "next/link"

import { useAuth } from "@/lib/auth"
import { canAccess, AppFeature } from "@/lib/types"
import { NavMain } from "@/components/nav-main"
import { NavUser } from "@/components/nav-user"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar"

const data = {
  navMain: [
    {
      title: "Tài khoản",
      url: "#",
      icon: IconCashPlus,
      feature: "accounts" as AppFeature,
      isActive: false,
      items: [
        {
          title: "Danh sách tài khoản",
          url: "/account"
        },
        {
          title: "Thêm tài khoản",
          url: "/account/created"
        }
      ]
    },
    {
      title: "Camera",
      url: "/camera",
      icon: IconListDetails,
      feature: "camera" as AppFeature,
    },
    {
      title: "Vi phạm",
      url: "#",
      icon: IconXboxX ,
      feature: "violations" as AppFeature,
      isActive: false,
      items: [
        {
        title: "Danh sách vi phạm",
        url: "/violation"
        }
      ]
    },
    {
      title: "Cài đặt",
      url: "/system",
      icon: IconAutomation,
      feature: "settings" as AppFeature,
    }
  ],
  navClouds: [
    {
      title: "Capture",
      icon: IconCamera,
      isActive: true,
      url: "#",
      items: [
        {
          title: "Active Proposals",
          url: "#",
        },
        {
          title: "Archived",
          url: "#",
        },
      ],
    },
  ]
}

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  const { user } = useAuth()

  // Lọc danh sách menu dựa trên quyền của user
  const filteredNavMain = React.useMemo(() => {
    return data.navMain.filter(item => canAccess(user, item.feature));
  }, [user]);

  return (
    <Sidebar collapsible="offcanvas" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          {canAccess(user, 'dashboard') && (
            <SidebarMenuItem>
              <SidebarMenuButton
                asChild
                className="data-[slot=sidebar-menu-button]:p-1.5!"
              >
                <Link href="/dashboard">
                <IconInnerShadowTop className="size-5! text-primary" />
                  <span className="text-base font-semibold">Dashboard</span>
                </Link>
              </SidebarMenuButton>
            </SidebarMenuItem>
          )}
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent >
        <NavMain items={filteredNavMain} />
      </SidebarContent>
      <SidebarFooter>
        {user && (
          <NavUser
            user={{
              name: user.username,
              email: user.email || '',
              avatar: "/avatars/shadcn.jpg", 
            }}
          />
        )}
      </SidebarFooter>
    </Sidebar>
  )
}
