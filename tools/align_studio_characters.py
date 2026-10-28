"""Measure source-to-cutout registration; writes JSON only, never edits artwork.

Optional authoring dependency: opencv-python-headless==4.10.0.84. The runtime
uses the saved layer rectangle, so end users do not need Python or OpenCV.
"""
import hashlib
import json
from pathlib import Path
import cv2
import numpy as np
from author_studio_library import sources, OUT, ASSETS
from library_directions import D

def measure(row):
    id=row['id'];cutout=ASSETS/'studio-library'/'art'/f'{id}-character.png'
    original=cv2.imdecode(np.fromfile(ASSETS/row['image'],np.uint8),cv2.IMREAD_COLOR)
    extracted=cv2.imdecode(np.fromfile(cutout,np.uint8),cv2.IMREAD_UNCHANGED)
    if extracted is None or extracted.shape[2]!=4:raise ValueError(f'{id}: missing alpha cutout')
    mask=cv2.erode((extracted[:,:,3]>240).astype(np.uint8)*255,np.ones((5,5),np.uint8))
    sift=cv2.SIFT_create(nfeatures=9000,contrastThreshold=.015)
    ka,da=sift.detectAndCompute(cv2.cvtColor(extracted[:,:,:3],cv2.COLOR_BGR2GRAY),mask)
    kb,db=sift.detectAndCompute(cv2.cvtColor(original,cv2.COLOR_BGR2GRAY),None)
    pairs=cv2.BFMatcher().knnMatch(da,db,k=2)
    good=[a for a,b in pairs if a.distance<.70*b.distance]
    if len(good)<16:raise ValueError(f'{id}: insufficient matching features ({len(good)})')
    source=np.float32([ka[m.queryIdx].pt for m in good]);target=np.float32([kb[m.trainIdx].pt for m in good])
    matrix,inliers=cv2.estimateAffinePartial2D(source,target,method=cv2.RANSAC,ransacReprojThreshold=3,maxIters=8000,confidence=.999)
    if matrix is None:raise ValueError(f'{id}: cannot register character')
    selected=inliers[:,0].astype(bool);a=source[selected];b=target[selected]
    if len(a)<12:raise ValueError(f'{id}: too few registration inliers ({len(a)})')
    rotation=np.degrees(np.arctan2(matrix[1,0],matrix[0,0]))
    if abs(rotation)>.6:raise ValueError(f'{id}: unexpected character rotation {rotation:.3f}')
    sx,tx=np.linalg.lstsq(np.column_stack([a[:,0],np.ones(len(a))]),b[:,0],rcond=None)[0]
    sy,ty=np.linalg.lstsq(np.column_stack([a[:,1],np.ones(len(a))]),b[:,1],rcond=None)[0]
    error=np.linalg.norm(a*np.array([sx,sy])+np.array([tx,ty])-b,axis=1)
    if not .3<sx<2 or not .3<sy<2 or abs(sx/sy-1)>.035 or np.quantile(error,.95)>4:
        raise ValueError(f'{id}: extraction deforms subject; scale {sx:.4f}/{sy:.4f}, p95 {np.quantile(error,.95):.2f}px')
    h,w=original.shape[:2];ch,cw=extracted.shape[:2]
    rect=[float(tx/w),float(ty/h),float(sx*cw/w),float(sy*ch/h)]
    # Identity avoids subpixel blur when no meaningful reframing occurred.
    if max(abs(tx),abs(ty),abs(sx*cw-w),abs(sy*ch-h))<1.25:rect=[0,0,1,1]
    result=dict(id=id,rect=[round(v,8) for v in rect],matches=len(good),inliers=len(a),medianErrorPx=round(float(np.median(error)),3),p95ErrorPx=round(float(np.quantile(error,.95)),3),
        originalSha256=hashlib.sha256((ASSETS/row['image']).read_bytes()).hexdigest(),characterSha256=hashlib.sha256(cutout.read_bytes()).hexdigest())
    print(f'{id}: {len(a)}/{len(good)} inliers; p95 {result["p95ErrorPx"]:.2f}px; rect {result["rect"]}')
    return result

def main():
    rows=[r for r in sources() if D[r['id']].get('character')]
    results=[];failures=[]
    for row in rows:
        try:results.append(measure(row))
        except Exception as error:failures.append(str(error));print('REVIEW:',error)
    (OUT/'alignment.json').write_text(json.dumps(dict(schema=1,characters=results,review=failures),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    if failures:raise SystemExit(f'{len(failures)} characters need alignment review')

if __name__=='__main__':main()
