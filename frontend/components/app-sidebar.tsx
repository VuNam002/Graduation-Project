"use client"

import * as React from "react"
import {
  IconCamera,
  IconInnerShadowTop,
  IconListDetails,
  IconCashPlus ,
  IconXboxX ,
  IconAutomation,
  IconLayoutDashboard 
} from "@tabler/icons-react"
import { GiCctvCamera } from "react-icons/gi";
import Link from "next/link"

import { useAuth } from "@/lib/auth"
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
        url: "/violation"
        }
      ]
    },
    {
      title: "Nhân viên",
      url: "#",
      icon: IconLayoutDashboard,
      isActive: false,
      items: [
        {
        title: "Danh sách nhân viên",
        url: "/employee"
        },
        {
        title: "Thêm nhân viên",
        url: "/employee/created"
        }
      ]
    },
    {
      title: "Cài đặt",
      url: "/system",
      icon: IconAutomation,
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
