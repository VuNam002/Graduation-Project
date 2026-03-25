"use client"

import { useEffect, useState } from "react"
import {
  IconCreditCard,
  IconDotsVertical,
  IconLogout,
  IconUserCircle,
} from "@tabler/icons-react"
import Link from "next/link"

import { useAuth } from "@/lib/auth"
import { fetchMeAccount } from "@/lib/api"
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
} from "@/components/ui/avatar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar"
import { MeAccountResponse } from "@/lib/types"


export function NavUser({
  user: defaultUser,
}: {
  user: {
    name: string
    email: string
    avatar: string
  }
}) {
  const { isMobile } = useSidebar()
  const { logout } = useAuth()
  const [account, setAccount] = useState<MeAccountResponse | null>(null)

  useEffect(() => {
    const getProfile = async () => {
      try {
        const res: any = await fetchMeAccount()
        if (res.data) {
          setAccount(res.data)
        } else {
          setAccount(res)
        }
      } catch (error) {
        console.error("Failed to fetch profile in NavUser:", error)
      }
    }
    getProfile()
  }, [])

  const displayName = account?.fullName || account?.username || defaultUser.name
  const displayEmail = account?.username || defaultUser.email

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton
              size="lg"
              className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
            >
              <Avatar className="h-8 w-8 rounded-lg grayscale">
                <AvatarImage src={defaultUser.avatar} alt={displayName} />
                <AvatarFallback className="rounded-lg">{displayName?.substring(0, 2).toUpperCase() || 'CN'}</AvatarFallback>
              </Avatar>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-medium">{displayName}</span>
                <span className="truncate text-xs text-muted-foreground">
                  {displayEmail}
                </span>
              </div>
              <IconDotsVertical className="ml-auto size-4" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            className="w-(--radix-dropdown-menu-trigger-width) min-w-56 rounded-lg"
            side={isMobile ? "bottom" : "right"}
            align="end"
            sideOffset={4}
          >
            <DropdownMenuLabel className="p-0 font-normal">
              <div className="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
                <Avatar className="h-8 w-8 rounded-lg">
                  <AvatarImage src={defaultUser.avatar} alt={displayName} />
                  <AvatarFallback className="rounded-lg">{displayName?.substring(0, 2).toUpperCase() || 'CN'}</AvatarFallback>
                </Avatar>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-medium">{displayName}</span>
                  <span className="truncate text-xs text-muted-foreground">
                    {displayEmail}
                  </span>
                </div>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem asChild>
                <Link href="/me" className="cursor-pointer">
                  <IconUserCircle />
                  Tài khoản
                </Link>
              </DropdownMenuItem>    
              <DropdownMenuItem asChild >
                <Link href="/me/update" className="cursor-pointer">
                  <IconCreditCard />
                  Cập nhật tài khoản  
                </Link>
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={logout}>
              <IconLogout />
              Đăng xuất
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
