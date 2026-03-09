"use client"

import * as React from "react"
import {
  IconCamera,
  IconHelp,
  IconInnerShadowTop,
  IconListDetails,
  IconSearch,
  IconSettings,
  IconCashPlus ,
  IconXboxX ,
} from "@tabler/icons-react"
import Link from "next/link"

import { useAuth } from "@/lib/auth"
import { NavMain } from "@/components/nav-main"
import { NavSecondary } from "@/components/nav-secondary"
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
    },
    {
      title: "Vi phạm",
      url: "#",
      icon: IconXboxX ,
      isActive: false,
      items: [
        {
        title: "Danh sách vi phạm",
        url: "/violations"
        }
      ]
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
  ],
  navSecondary: [
    {
      title: "Settings",
      url: "#",
      icon: IconSettings,
    },
    {
      title: "Get Help",
      url: "#",
      icon: IconHelp,
    },
    {
      title: "Search",
      url: "#",
      icon: IconSearch,
    },
  ],
 
}

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  const { user } = useAuth()

  return (
    <Sidebar collapsible="offcanvas" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              asChild
              className="data-[slot=sidebar-menu-button]:p-1.5!"
            >
              <Link href="/dashboard">
                <IconInnerShadowTop className="size-5!" />
                <span className="text-base font-semibold">Dashboard</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent >
        <NavMain items={data.navMain} />
        <NavSecondary items={data.navSecondary} className="mt-auto" />
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
