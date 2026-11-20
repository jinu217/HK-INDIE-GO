def mesh(name,verts,faces,mat,group='01_Floor',uv=None,smooth=False):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);collection(group).objects.link(o)
    if mat:me.materials.append(mat)
    layer=me.uv_layers.new(name='UVMap')
    for poly in me.polygons:
        poly.use_smooth=smooth
        for li in poly.loop_indices:
            vi=me.loops[li].vertex_index
            if uv is not None:layer.data[li].uv=uv[vi]
            else:
                co=me.vertices[vi].co
                layer.data[li].uv=(.22+(co.x%1)*.10,.23+(co.z%1)*.10)
    return o

def bevel(o,width=.02,segments=3):
    m=o.modifiers.new('Soft_carved_edges','BEVEL');m.width=width;m.segments=segments
    return o

def cube(name,loc,scale,mat=ivory,group='02_Architecture',bev=.012):
    x,y,z=loc;a,b,c=[s/2 for s in scale]
    v=[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),(x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
    f=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
    o=mesh(name,v,f,mat,group)
    if bev:bevel(o,bev)
    return o

def curve(name,pts,r,mat=gold,group='03_Goldwork',cyclic=False):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=2;c.bevel_depth=r;c.bevel_resolution=2
    sp=c.splines.new('POLY');sp.points.add(len(pts)-1)
    for p,co in zip(sp.points,pts):p.co=(*co,1)
    sp.use_cyclic_u=cyclic
    o=bpy.data.objects.new(name,c);collection(group).objects.link(o);c.materials.append(mat);return o

def bezier_pts(p0,p1,p2,p3,n=24):
    a,b,c,d=[Vector(p) for p in [p0,p1,p2,p3]]
    return [tuple((1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d) for t in [i/(n-1) for i in range(n)]]

def lathe(name,x,y,profile,mat=ivory,group='02_Architecture',n=32):
    v=[];uv=[];f=[]
    for k,(z,r) in enumerate(profile):
        for j in range(n):
            a=2*PI*j/n;v.append((x+r*math.cos(a),y+r*math.sin(a),z));uv.append((.22+j/n*.10,.23+k/(len(profile)-1)*.10))
    for k in range(len(profile)-1):
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n;c=(k+1)*n+(j+1)%n;d=(k+1)*n+j
            if profile[k][1]==0:f.append((a,c,d))
            elif profile[k+1][1]==0:f.append((a,b,c))
            else:f.append((a,b,c,d))
    return mesh(name,v,f,mat,group,uv,True)

def railing(path,name):
    # Cross-section swept along curved or straight path, not independent boxes.
    g='03_Balustrades'
    def sweep(z,width,height,label,mat=ivory):
        verts=[];faces=[];uv=[]
        for i,p in enumerate(path):
            tangent=Vector(path[min(i+1,len(path)-1)])-Vector(path[max(0,i-1)])
            tangent.normalize();n=Vector((-tangent.y,tangent.x))
            for off,h in [(-width/2,-height/2),(width/2,-height/2),(width/2,height/2),(-width/2,height/2)]:
                verts.append((p[0]+n.x*off,p[1]+n.y*off,z+h));uv.append((.22+i/len(path)*.1,.23+(h/height+.5)*.1))
        for i in range(len(path)-1):
            for j in range(4):faces.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
        faces.extend([(3,2,1,0),tuple((len(path)-1)*4+j for j in range(4))])
        return bevel(mesh(name+label,verts,faces,mat,g,uv),.012,3)
    sweep(.12,.23,.14,'_LowerRail');sweep(.755,.285,.105,'_Handrail')
    sweep(.807,.22,.012,'_GoldRibbon',gold_light)
    sweep(.696,.20,.025,'_HandrailUnderlip')
    length=sum((Vector(path[i])-Vector(path[i-1])).length for i in range(1,len(path)))
    count=int(length/.32)
    for k in range(count):
        desired=(k+.5)*length/count;dist=0
        for j in range(1,len(path)):
            seg=(Vector(path[j])-Vector(path[j-1])).length
            if dist+seg>=desired:
                t=(desired-dist)/seg;p=Vector(path[j-1]).lerp(Vector(path[j]),t);break
            dist+=seg
        x,y=p
        profile=[(.18,.073),(.205,.084),(.235,.075),(.255,.052),(.28,.049),(.32,.073),(.37,.09),(.415,.075),(.46,.05),(.515,.04),(.57,.047),(.605,.065),(.635,.082),(.68,.082),(.70,.069)]
        lathe(name+f'_Baluster{k:02}',x,y,profile,ivory,g,24)