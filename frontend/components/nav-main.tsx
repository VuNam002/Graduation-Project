"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import { IconChevronRight, type Icon } from "@tabler/icons-react"

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible"
import {
  SidebarGroup,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
} from "@/components/ui/sidebar"

export function NavMain({
  items,
}: {
  items: {
    title: string
    url: string
    icon?: Icon
    isActive?: boolean
    items?: {
      title: string
      url: string
    }[]
  }[]
}) {
  const pathname = usePathname()

  const checkActive = (itemUrl: string, subItems?: { url: string }[]) => {
    if (pathname === itemUrl) return true
    return subItems?.some(sub => pathname === sub.url)
  }

  return (
    <SidebarGroup>
      <SidebarGroupContent className="flex flex-col gap-2">
        <SidebarMenu>
          {items.map((item) => {
            const isActive = checkActive(item.url, item.items)
            
            return item.items && item.items.length > 0 ? (
              <Collapsible
                key={item.title}
                asChild
                defaultOpen={isActive || item.isActive}
                className="group/collapsible"
              >
                <SidebarMenuItem>
                  <CollapsibleTrigger asChild>
                    <SidebarMenuButton 
                      tooltip={item.title} 
                      isActive={isActive}
                      className={isActive ? "border-l-4 border-primary rounded-l-none bg-sidebar-accent/50" : ""}
                    >
                      {item.icon && <item.icon className="text-primary size-5" />}
                      <span className="text-base font-medium">{item.title}</span>
                      <IconChevronRight className="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
                    </SidebarMenuButton>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <SidebarMenuSub>
                      {item.items.map((subItem) => {
                        const isSubActive = pathname === subItem.url
                        return (
                          <SidebarMenuSubItem key={subItem.title}>
                            <SidebarMenuSubButton asChild isActive={isSubActive}>
                            <Link href={subItem.url}>
                                <span className={`font-medium ${isSubActive ? "text-primary font-bold" : ""}`}>
                                  {subItem.title}
                                </span>
                            </Link>
                          </SidebarMenuSubButton>
                        </SidebarMenuSubItem>
                        )
                      })}
                    </SidebarMenuSub>
                  </CollapsibleContent>
                </SidebarMenuItem>
              </Collapsible>
            ) : (
              <SidebarMenuItem key={item.title}>
                <SidebarMenuButton 
                  asChild 
                  tooltip={item.title}
                  isActive={isActive}
                  className={isActive ? "border-l-4 border-primary rounded-l-none bg-sidebar-accent/50" : ""}
                >
                  <Link href={item.url}>
                    {item.icon && <item.icon className="text-primary size-5" />}
                    <span className="text-base font-medium">{item.title}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            )
          })}
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  )
}
