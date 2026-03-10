"use client"

import React, { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { fetchDetailViolation, getBackendUrl } from '@/lib/api';
import { ViolationLog } from '@/lib/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { ArrowLeft, Calendar, AlertTriangle, CheckCircle, XCircle, Activity, Tag, Maximize, Info } from 'lucide-react';

export default function ViolationDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params?.id ? Number(params.id) : null;

  const [violation, setViolation] = useState<ViolationLog | null>(null);
  const [loading, setLoading] = useState(true);
  const [imgSize, setImgSize] = useState<{ w: number; h: number } | null>(null);

  useEffect(() => {
    if (id) {
      fetchDetailViolation(id)
        .then((data) => {
          setViolation(data);
        })
        .catch((err) => console.error("Failed to fetch violation details:", err))
        .finally(() => setLoading(false));
    }
  }, [id]);

  const handleBack = () => {
    router.back();
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  };

  const getStatusBadge = (status: number, text?: string) => {
    switch (status) {
      case 0: 
        return <Badge variant="destructive" className="text-sm px-3 py-1">{text || 'Mới'}</Badge>;
      case 1: 
        return <Badge variant="secondary" className="text-sm px-3 py-1">{text || 'Đang xử lý'}</Badge>;
      case 2: 
        return <Badge variant="outline" className="bg-green-100 text-green-800 border-green-200 text-sm px-3 py-1">{text || 'Đã giải quyết'}</Badge>;
      default:
        return <Badge variant="outline">{text || 'Không xác định'}</Badge>;
    }
  };

  if (loading) {
    return (
      <div className="container mx-auto p-6 space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-10 w-10 rounded-full" />
          <Skeleton className="h-8 w-64" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <Skeleton className="lg:col-span-2 h-[500px] rounded-xl" />
          <Skeleton className="h-[500px] rounded-xl" />
        </div>
      </div>
    );
  }

  if (!violation) {
    return (
      <div className="container mx-auto p-6 text-center">
        <h2 className="text-2xl font-bold text-red-600">Không tìm thấy vi phạm</h2>
        <Button onClick={handleBack} className="mt-4">Quay lại</Button>
      </div>
    );
  }

  const imageUrl = violation.imagePath.startsWith('http') 
    ? violation.imagePath 
    : `${getBackendUrl()}${violation.imagePath.startsWith('/') ? '' : '/'}${violation.imagePath}`;

  return (
    <div className="container mx-auto p-4 md:p-6 max-w-7xl">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <Button variant="outline" size="icon" onClick={handleBack} className="h-10 w-10">
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-2xl md:text-3xl font-bold tracking-tight flex items-center gap-3">
            Chi tiết vi phạm #{violation.id}
            {getStatusBadge(violation.status, violation.statusText)}
          </h1>
          <p className="text-muted-foreground flex items-center gap-2 mt-1">
            <Calendar className="h-4 w-4" />
            {formatDate(violation.detectedTime)}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2 overflow-hidden border-2">
          <CardContent className="p-0 relative bg-black/5 flex items-center justify-center min-h-[400px]">
            <div className="relative w-full h-full">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img 
                src={imageUrl} 
                alt={`Violation ${violation.id}`}
                className="w-full h-auto object-contain max-h-[70vh] mx-auto"
                onLoad={(e) => {
                  setImgSize({ w: e.currentTarget.naturalWidth, h: e.currentTarget.naturalHeight });
                }}
              />

              {imgSize && violation.box && (
                <svg 
                  viewBox={`0 0 ${imgSize.w} ${imgSize.h}`} 
                  className="absolute top-0 left-0 w-full h-full pointer-events-none"
                  style={{ zIndex: 10 }}
                >
                  <rect 
                    x={violation.box.x} 
                    y={violation.box.y} 
                    width={violation.box.width} 
                    height={violation.box.height} 
                    fill="none" 
                    stroke={violation.colorCode || "#ef4444"} 
                    strokeWidth={Math.max(2, imgSize.w / 200)} 
                    strokeDasharray="4"
                  />
                </svg>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Right Column: Details */}
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Info className="h-5 w-5 text-primary" />
                Thông tin chi tiết
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 gap-4">
                <div className="p-3 bg-muted/50 rounded-lg">
                  <span className="text-sm text-muted-foreground flex items-center gap-2 mb-1"><Tag className="h-4 w-4" /> Loại vi phạm</span>
                  <div className="font-semibold text-lg">{violation.displayName || violation.categoryId}</div>
                </div>
                
                <div className="p-3 bg-muted/50 rounded-lg">
                  <span className="text-sm text-muted-foreground flex items-center gap-2 mb-1"><Activity className="h-4 w-4" /> Độ tin cậy</span>
                  <div className="font-semibold text-lg">
                    {violation.confidence ? `${violation.confidence.toFixed(2)}%` : 'N/A'}
                  </div>
                </div>

                <div className="p-3 bg-muted/50 rounded-lg">
                  <span className="text-sm text-muted-foreground flex items-center gap-2 mb-1"><AlertTriangle className="h-4 w-4" /> Mức độ nghiêm trọng</span>
                  <div className="flex items-center gap-2">
                    <div 
                      className="h-3 w-3 rounded-full" 
                      style={{ backgroundColor: violation.colorCode || '#ef4444' }}
                    />
                    <span className="font-semibold">Mức {violation.severityLevel || 'N/A'}</span>
                  </div>
                </div>

                <div className="p-3 bg-muted/50 rounded-lg">
                  <span className="text-sm text-muted-foreground flex items-center gap-2 mb-1"><Maximize className="h-4 w-4" /> Tọa độ (Box)</span>
                  <div className="text-sm font-mono">
                    {violation.box ? (
                      <>
                        X: {violation.box.x.toFixed(0)}, Y: {violation.box.y.toFixed(0)}<br/>
                        W: {violation.box.width.toFixed(0)}, H: {violation.box.height.toFixed(0)}
                      </>
                    ) : 'Không có dữ liệu'}
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Actions (Placeholder for future features) */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Hành động</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-3">
              <Button className="w-full" variant="default">
                <CheckCircle className="mr-2 h-4 w-4" /> Xác nhận vi phạm
              </Button>
              <Button className="w-full" variant="outline">
                <XCircle className="mr-2 h-4 w-4" /> Báo cáo sai
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}